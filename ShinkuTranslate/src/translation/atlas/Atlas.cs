﻿using ShinkuTranslate.misc;
using ShinkuTranslate.settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ShinkuTranslate.translation.atlas {
    class Atlas : WithInitialization {
        private static Atlas _instance = new Atlas();

        public static Atlas instance {
            get {
                _instance.tryWaitForInitialization();
                return _instance;
            }
        }

        public bool isNotFound { get { return interop == null ? false : interop.atlasNotFound; } }

        protected Atlas() {
        }

        private AtlasInterop interop;
        private IntPtr buf1;
        private IntPtr buf2;
        private byte[] envStr;
        private Encoding encoding932;

        protected override void doInitialize()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            encoding932 = Encoding.GetEncoding(932);
            interop = new AtlasInterop();
            try
            {
                interop.initialize();
                createEngine();
            }
            catch
            {
                interop.close();
                throw;
            }
        }

        private void createEngine() {
            buf1 = Marshal.AllocHGlobal(30000);
            buf2 = Marshal.AllocHGlobal(30000);
            if (interop.atlInitEngineData(0, 2, buf1, 0, buf2, 0, 0, 0, 0) != 0)
                throw new MyException("ATLAS AtlInitEngineData failed");
            envStr = encoding932.GetBytes(Settings.app.atlasEnv);
            if (interop.createEngine(1, 1, 0, envStr) != 1)
                throw new MyException("ATLAS CreateEngine failed");
        }

        private static char normalizeStopChar(char c) {
            switch (c) {
                case '。':
                    return '.';
                case '？':
                    return '?';
                case '！':
                    return '!';
                default:
                    return c;
            }
        }

        private readonly Dictionary<char, char> openAndClose = new Dictionary<char, char> { { '『', '』' }, { '「', '」' }, { '【', '】' } };
        
        public string translate(string src) {
            if (state != State.WORKING) {
                return null;
            }
            src = src.Trim();
            if (src == "") {
                return src;
            }
            if (src.EndsWith("』") || src.EndsWith("」") || src.EndsWith("）")) {
                char openBr;
                if (src.EndsWith("』")) {
                    openBr = '『';
                } else if (src.EndsWith("」")) {
                    openBr = '「';
                } else {
                    openBr = '（';
                }
                int start = src.IndexOf(openBr);
                if (start != -1) {
                    string first = src.Substring(0, start);
                    string second = src.Substring(start + 1, src.Length - start - 2);
                    string firstTr;
                    if (first != "") {
                        firstTr = translate(first) + (Settings.app.separateSpeaker ? "\n" : " ");
                    } else {
                        firstTr = "";
                    }
                    string secondTr = translate(second);
                    return firstTr + openBr + secondTr + src[src.Length - 1];
                }
            }
            src = Regex.Replace(src, @"([\u3040-\u309F])ー", (m) => {
                char prev = m.Groups[1].Value[0];
                char cur = 'ー';
                string roma = HiraganaConvertor.instance.ConvertLetter(prev);
                if (roma != null) {
                    if (roma.EndsWith("a")) {
                        cur = 'あ';
                    } else if (roma.EndsWith("e")) {
                        cur = 'え';
                    } else if (roma.EndsWith("i")) {
                        cur = 'い';
                    } else if (roma.EndsWith("o") || roma.EndsWith("u")) {
                        cur = 'う';
                    }
                }
                return "" + prev + cur;
            });
            string newSrc = Regex.Replace(src, "…+", "、");
            if (!(newSrc == "、" && newSrc != src)) {
                src = newSrc;
            }
            var res = new StringBuilder();
            var buf = new StringBuilder();
            int i = 0;
            int ignoreStopCharsUntil = 0;
            while (i < src.Length) {
                char c = src[i];
                if (".?!。？！".IndexOf(c) != -1 && i >= ignoreStopCharsUntil) {
                    if (buf.Length == 0) {
                        res.Append(normalizeStopChar(c));
                        i += 1;
                    } else {
                        while (i < src.Length && ".?!。？！".IndexOf(src[i]) != -1) {
                            buf.Append(src[i]);
                            i += 1;
                        }
                        res.Append(translatePart(buf.ToString().Trim()));
                        res.Append(' ');
                        buf.Clear();
                    }
                } else if (openAndClose.ContainsKey(c)) {
                    int end = findMatchingBracket(src, i);
                    buf.Append(c);
                    i += 1;
                    if (ignoreStopCharsUntil < end) {
                        ignoreStopCharsUntil = end;
                    }
                } else {
                    buf.Append(c);
                    i += 1;
                }
            }
            if (buf.Length > 0) {
                res.Append(translatePart(buf.ToString().Trim()));
            }
            return res.ToString().Trim();
        }

        private int findMatchingBracket(string src, int i) {
            Stack<char> brackets = new Stack<char>();
            brackets.Push(openAndClose[src[i]]);
            i += 1;
            while (i < src.Length) {
                char c = src[i];
                if (c == brackets.Peek()) {
                    brackets.Pop();
                    if (brackets.Count == 0) {
                        return i;
                    }
                } else if (openAndClose.ContainsKey(c)) {
                    brackets.Push(openAndClose[c]);
                }
                i += 1;
            }
            return -1;
        }
        
        public string translatePart(string text) {
            if (state != State.WORKING) {
                return "[error]";
            }
            if (string.IsNullOrWhiteSpace(text)) {
                return text;
            }
            lock (this) {
                IntPtr outp;
                IntPtr tmp;
                uint size;
                byte[] inp = encoding932.GetBytes(text);
                interop.translatePair(inp, out outp, out tmp, out size);
                string result;
                if (outp != IntPtr.Zero) {
                    if (size > 5000) {
                        size = 5000;
                    }
                    byte[] data = new byte[size];
                    Marshal.Copy(outp, data, 0, (int)size);
                    int boundary = Array.IndexOf<byte>(data, 0);
                    if (boundary == -1) {
                        result = encoding932.GetString(data);
                    } else {
                        result = encoding932.GetString(data, 0, boundary);
                    }
                    interop.freeAtlasData(outp, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                } else {
                    result = "[error]";
                }
                return result;
            }
        }

        public void close() {
            if (state == State.WORKING) {
                interop.close();
            }
        }

        protected override void onInitializationError(Exception ex) {
        }

        internal void reinitialize() {
            if (state == State.WORKING) {
                Task.Factory.StartNew(() => {
                    deinitialize();
                    try {
                        state = State.INITIALIZING;
                        interop.destroyEngine();
                        createEngine();
                        state = State.WORKING;
                    } catch (Exception ex) {
                        Logger.logException(ex);
                        state = State.ERROR;
                    }
                    initialized.Set();
                });
            }
        }

        internal IEnumerable<string> getEnvList() {
            try {
                if (interop == null || interop.atlasNotFound) {
                    return Enumerable.Repeat("General", 1);
                } else {
                    string envFileName = Path.Combine(interop.installPath, "transenv.ini");
                    var sectionNames = Regex.Matches(File.ReadAllText(envFileName, encoding932), @"^\[(.+?)\]\s*$", RegexOptions.Multiline);
                    var res = (from match in sectionNames.Cast<Match>()
                            select match.Groups[1].Value).ToList();
                    return res;
                }
            } catch (Exception ex) {
                Logger.logException(ex);
                return Enumerable.Repeat("General", 1);
            }
        }

        internal string translateWithReplacements(string src) {
            ReplacementScript rs = ReplacementScript.get();
            string repl = rs.process(src);
            return translate(repl);
        }
    }
}
