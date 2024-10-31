using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using ShinkuTranslate.settings;
using ShinkuTranslate.misc;
using System.Collections;
using System.Windows.Forms;
using static System.Windows.Forms.DataFormats;

namespace ShinkuTranslate.translation.edict.inflect {
    class Inflector {
        Dictionary<string, ConjugationsJson> conjugations;
        Dictionary<string, InflectionTrie> index;

        public static ISet<string> knownPOS { get; private set; }

        internal void load() {
            try {
                conjugations = new Dictionary<string, ConjugationsJson>();
                knownPOS = new HashSet<string>();
                string jsonRaw = File.ReadAllText(Settings.app.ConjugationsPath);
                IList<IDictionary<string, object>> json = JsonSerializer.Deserialize<IList<IDictionary<string, object>>>(jsonRaw);
                foreach (IDictionary<string, object> conjJson in json) {
                    
                    List<ConjugationsVariantJson> tenses = new List<ConjugationsVariantJson>();

                    var jsonTenses = ((JsonElement)conjJson["Tenses"]).Deserialize<IList<IDictionary<string, dynamic>>>();
                    foreach (IDictionary<string, dynamic> form in jsonTenses) {
                        IDictionary<string, object> formJson = form;
                        ConjugationsVariantJson conjVar = new ConjugationsVariantJson
                        {
                            Formal = ((JsonElement)formJson["Formal"]).Deserialize<bool>(),
                            Negative = ((JsonElement)formJson["Negative"]).Deserialize<bool>(),
                            Suffix = ((JsonElement)formJson["Suffix"]).Deserialize<string>(),
                            Tense = ((JsonElement)formJson["Tense"]).Deserialize<string>(),

                            NextType = formJson.ContainsKey("Next Type")
                                ? ((JsonElement)formJson["Next Type"]).Deserialize<string>()
                                : (((JsonElement)formJson["Tense"]).Deserialize<string>() == "Te-form" ? "te-form" : null),

                            Ignore = formJson.ContainsKey("Ignore")
                                ? ((JsonElement)formJson["Ignore"]).Deserialize<bool>()
                                : false
                        };
                        //conjVar.Ignore = conjVar.Ignore || (conjVar.Tense == "Stem" && conjVar.Suffix == "");
                        tenses.Add(conjVar);
                    }
                    var conj = new ConjugationsJson {
                        Name = ((JsonElement)conjJson["Name"]).Deserialize<string>(),
                        PartOfSpeech = ((JsonElement)conjJson["Part of Speech"]).Deserialize<string>(),
                        Tenses = tenses
                    };
                    conj.addBaseFormSuffix(tenses[0].Suffix);
                    ConjugationsJson old;
                    if (conjugations.TryGetValue(conj.Name, out old)) {
                        old.addTenses(conj.Tenses);
                        old.addBaseFormSuffix(tenses[0].Suffix);
                    } else {
                        knownPOS.Add(conj.Name);
                        conjugations.Add(conj.Name, conj);
                    }
                }
                rebuildIndex();
            } catch (Exception ex) {
                Logger.logException(ex);
            }
        }

        private void rebuildIndex() {
            index = new Dictionary<string, InflectionTrie>();
            foreach (var conj in conjugations.Values) {
                var trie = new InflectionTrie();
                foreach (var form in conj.Tenses) {
                    trie.addForm(form, conjugations);
                }
                index[conj.Name] = trie;
            }
        }

        internal IEnumerable<InflectionState> findMatching(bool canUseKatakana, List<string> POS, string text, int position) {
            List<InflectionState> results = new List<InflectionState>();
            //Logger.log("Finding inflections in: " + text.Substring(position));
            List<Tuple<InflectionTrie, InflectionState, string>> cur = new List<Tuple<InflectionTrie,InflectionState,string>>();
            foreach (string pos in POS) {
                InflectionTrie trie;
                if (index.TryGetValue(pos, out trie)) {
                    cur.Add(Tuple.Create<InflectionTrie, InflectionState, string>(trie, null, pos));
                }
            }
            int offset = 0;
            bool hasEmptySuf = false;
            while (cur.Count > 0 && position + offset <= text.Length) {
                //Logger.log("POS List: " + string.Join(", ", cur.Select((q) => q.Item3)));
                List<Tuple<InflectionTrie, InflectionState, string>> added = new List<Tuple<InflectionTrie, InflectionState, string>>();
                List<Tuple<InflectionTrie, InflectionState, string>> added2 = new List<Tuple<InflectionTrie, InflectionState, string>>();
                List<Tuple<InflectionTrie, InflectionState, string>> next = new List<Tuple<InflectionTrie, InflectionState, string>>();
                HashSet<string> addedPOS = new HashSet<string>();
                foreach (var it in cur) {
                    foreach (var link in it.Item1.linkForms) {
                        if (addedPOS.Add(link.NextType)) {
                            InflectionState linked = new InflectionState("", link, it.Item2 == null ? null : it.Item2.tense);
                            added.Add(Tuple.Create(index[link.NextType], linked, it.Item3));
                            //Logger.log("Added: " + link.NextType);
                        }
                    }
                }
                foreach (var it in added) {
                    foreach (var link in it.Item1.linkForms) {
                        if (addedPOS.Add(link.NextType)) {
                            InflectionState linked = new InflectionState("", link, it.Item2 == null ? null : it.Item2.tense);
                            added2.Add(Tuple.Create(index[link.NextType], linked, it.Item3));
                            //Logger.log("Added2: " + link.NextType);
                        }
                    }
                }
                InflectionState newState = null;
                char c;
                if (position + offset < text.Length) {
                    c = text[position + offset];
                    if (canUseKatakana) {
                        c = TextUtils.katakanaToHiraganaChar(c);
                    }
                } else {
                    c = '\0';
                }
                foreach (var it in cur.Concat(added).Concat(added2)) {
                    foreach (var form in it.Item1.forms) {
                        //Logger.log("Got form: " + form.Suffix + " (" + it.Item3 + ")");
                        if (newState == null) {
                            newState = new InflectionState(text.Substring(position, offset), form, it.Item2 == null ? null : it.Item2.tense);
                        } else {
                            newState.updateTense(form.Tense);
                        }
                        newState.addPOS(it.Item3);
                    }
                    InflectionTrie nextTrie;
                    if (it.Item1.children.TryGetValue(c, out nextTrie)) {
                        //Logger.log(it.Item3 + ": going deeper to " + c);
                        next.Add(Tuple.Create(nextTrie, it.Item2, it.Item3));
                    }
                }
                if (newState != null) {
                    if (!newState.suffix.EndsWith("てい") && !newState.suffix.EndsWith("でい")
                        && !newState.suffix.EndsWith("るた")) { // dirty HACK. bad bad me :(
                        if (newState.suffix == "") {
                            hasEmptySuf = true;
                        }
                        results.Add(newState);
                    }
                }
                cur = next;
                offset += 1;
            }
            if (!hasEmptySuf && (POS == null || !knownPOS.IsSupersetOf(POS))) { 
                InflectionState state = new InflectionState("");
                results.Add(state);
            }
            return results;
        }

        internal string getStem(string baseForm, IEnumerable<string> POS) {
            foreach (string pos in POS) {
                ConjugationsJson conj;
                if (conjugations.TryGetValue(pos, out conj)) {
                    foreach (string suffix in conj.BaseFormSuffixes) {
                        if (baseForm.EndsWith(suffix)) {
                            string res = baseForm.Substring(0, baseForm.Length - suffix.Length);
                            /*if (res.Length == 0) {
                                Logger.log(baseForm);
                            }*/
                            return res;
                        }
                    }
                    return baseForm;
                }
            }
            return baseForm;
        }

        internal IEnumerable<string> getAllSuffixes(IEnumerable<string> POS) {
            HashSet<string> suffixes = new HashSet<string>();
            foreach (string aPOS in POS) {
                var conj = conjugations.GetOrDefault(aPOS);
                if (conj != null) {
                    suffixes.UnionWith(conj.Tenses.Select((t) => t.Suffix));
                }
            }
            return suffixes;
        }
    }
}
