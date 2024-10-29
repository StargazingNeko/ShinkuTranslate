(function() {
  var evalAsJson, get, wrap;

  evalAsJson = function(json) {
    var fn;
    fn = new Function("return " + json);
    return fn();
  };

  get = function(options, callback, func) {
    return http.request(options, function(res, err) {
      var result;
      if (err) {
        return callback(err);
      } else {
        try {
          result = func(res);
        } catch (ex) {
          result = ex.toString();
        }
        return callback(result);
      }
    });
  };

  wrap = function(fn) {
    return function(src, callback, ex) {
      var fixedCallback, fixedSrc, q;
      q = /(.*?)([「『（][\s\S]*[」』）])\s*$/.exec(src);
      if (q) {
        fixedSrc = q[2].substr(1, q[2].length - 2);
        fixedCallback = function(res) {
          res = q[2].charAt(0) + res + q[2].charAt(q[2].length - 1);
          return callback(res.replace('<unk>', ' '));
        };
        return fn(fixedSrc, fixedCallback, ex);
      } else {
        return fn(src, callback, ex);
      }
    };
  };

  registerTranslators({
    "Google": wrap(function(src, callback) {
      var url;
      src = encodeURIComponent(src);
      url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=ja&tl=en&dt=t&q=" + src;
      return get(url, callback, function(res) {
        var s, ss;
        res = evalAsJson(res);
        ss = (function() {
          var _i, _len, _ref, _results;
          _ref = res[0];
          _results = [];
          for (_i = 0, _len = _ref.length; _i < _len; _i++) {
            s = _ref[_i];
            _results.push($.trim(s[0]));
          }
          return _results;
        })();
        return ss.join(' ').replace(/\btsu\b/ig, '');
      });
    }),
	"Sugoi": wrap(function(src, callback) {
      var requestBody = JSON.stringify({t: src});
      var url = "http://127.0.0.1:8080/translate";

      try {
        var xhr = new XMLHttpRequest();
        xhr.open("POST", url, true);
        xhr.setRequestHeader("Content-Type", "application/json");
        xhr.onreadystatechange = function() {
          if (xhr.readyState === XMLHttpRequest.DONE) {
            if (xhr.status === 200) {
              callback(JSON.parse(xhr.responseText));
            } else {
              console.error('Error:', xhr.statusText);
              callback("Error occurred: " + xhr.statusText);
            }
          }
        };
        xhr.send(requestBody);
      } catch (error) {
        console.error('Error:', error.message);
        callback("Error occurred: " + error.message);
      }
    }),
    "Ollama": wrap(function(src, callback) {
  var prompt = "<<METADATA>>\n[character] Name: Kanade Taiga (奏 大雅) | Gender: Male\n[character] Name: Kuro (クロ) | Gender: Male\n<<TRANSLATE>>\n<<JAPANESE>>\n" + src + "\n<<ENGLISH>>";
  var requestBody = JSON.stringify({
    model: "vntl",
    prompt: prompt,
    "options": {
      "temperature": 0
    },
    stream: false
  });
  var url = "http://localhost:11434/api/generate";

  var xhr = new XMLHttpRequest();
  xhr.open("POST", url, true);
  xhr.setRequestHeader("Content-Type", "application/json");

  xhr.onload = function() {
    if (xhr.status === 200) {
      try {
        var response = JSON.parse(xhr.responseText);
        callback(response.response);
      } catch (error) {
        console.error('Error:', error.message);
        callback("Error occurred: " + error.message);
      }
    } else {
      console.error('Error:', xhr.statusText);
      callback("Error occurred: " + xhr.statusText);
    }
  };

  xhr.onerror = function() {
    console.error('Error:', xhr.statusText);
    callback("Error occurred: " + xhr.statusText);
  };

  xhr.send(requestBody);
})
	

  });

}).call(this);
