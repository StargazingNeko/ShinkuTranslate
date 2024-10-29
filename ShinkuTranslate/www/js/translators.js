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
      var url = "http://127.0.0.1:6969/translate";

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
    "Deepseek": wrap(function(src, callback) {
      var url = "https://api.deepseek.com/v1/chat/completions";
      var requestBody = JSON.stringify({
        model: "deepseek-chat",
        messages: [
          {
            role: "system",
            content: "Translate everything to English, no explanation, no whatever, just output the English translation"
          },
          {
            role: "user",
            content: src
          }
        ]
      });

      try {
        var xhr = new XMLHttpRequest();
        xhr.open("POST", url, true);
        xhr.setRequestHeader("Content-Type", "application/json");
		xhr.setRequestHeader("Authorization", "Bearer DEEPSEEK_API_KEY_HERE");
        xhr.onreadystatechange = function() {
          if (xhr.readyState === XMLHttpRequest.DONE) {
            if (xhr.status === 200) {
              var response = JSON.parse(xhr.responseText);
              callback(response.choices[0].message.content.trim());
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
    })
	

  });

}).call(this);
