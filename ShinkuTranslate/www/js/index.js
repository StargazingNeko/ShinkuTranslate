(function() {
  var clearContexts, connectError, connectSuccess, contexts, disableContexts, disconnect, formatAddr, formatProcessName, isSelectWindow, log, logParse, longToHex, newContext, newSentence, selectWindowEnd, setDefaultProcess, updateProcesses;

  isSelectWindow = false;

  $(function() {
    $('.showTranslation').click(function() {
      return host().showTranslationForm();
    });
	$(document).ready(function () {
      return host().showTranslationForm();
    });
    $('.options').click(function() {
      return host().showOptions();
    });
    $('#about').click(function() {
      return host().showAbout(function() {});
    });
    $('#contexts').on('change', '.check input', function() {
      var ctx, ctxId;
      ctxId = $(this).data('id');
      ctx = contexts[ctxId];
      if (ctx != null) {
        ctx.enabled = $(this).prop('checked');
        return host().setContextEnabled(ctx.id, ctx.enabled);
      }
    }).on('dblclick', '.check input', function() {
      var c, ctx, ctxId, id;
      ctxId = $(this).data('id');
      ctx = contexts[ctxId];
      if (ctx != null) {
        $('#contexts .check input').not(this).prop('checked', false);
        for (id in contexts) {
          c = contexts[id];
          c.enabled = false;
        }
        ctx.enabled = true;
        $(this).prop('checked', true);
        return host().setContextEnabledOnly(ctx.id);
      }
    });
    $('#contexts').on('click', '.name_s', function() {
      var $this, ctxId, ofs;
      $this = $(this);
      ofs = $this.offset();
      ctxId = Number($this.data('id'));
      host().showLog(ctxId);
    }).on('click', '.text_s', function() {
      host().translate($(this).text());
    });
    return $('#newContexts').change(function() {
      return host().setNewContextsBehavior($('#newContexts').val());
    });
  });

  contexts = {};

  selectWindowEnd = window.selectWindowEnd = function() {
    isSelectWindow = false;
    return $('#connect_status').empty();
  };

  connectError = window.connectError = function(errMsg) {
    $('#connect').prop('disabled', false);
    $('#connect_status').html(errMsg);
  };

  clearContexts = function() {
    contexts = {};
    $('#contexts').empty();
  };

  log = window.log = function(s) {
    $('<div>').text(s).appendTo($('#log'));
  };

  logParse = window.logParse = function(s) {
    $('<div style="margin-bottom:1em"></div>').text(s).prependTo($('#parse'));
  };

  longToHex = function(addr) {
    var i, res, x;
    res = (function() {
      var _i, _results;
      _results = [];
      for (i = _i = 1; _i <= 8; i = ++_i) {
        x = addr % 16;
        addr = addr / 16 | 0;
        _results.push("0123456789ABCDEF".charAt(x));
      }
      return _results;
    })();
    return res.reverse().join('');
  };

  formatAddr = function(addr) {
    var addrText;
    addrText = longToHex(addr);
    return addrText.substr(0, 4) + ':' + addrText.substr(4);
  };

  newContext = window.newContext = function(id, name, addr, sub, enabled) {
    var ctx, nameStr;
    ctx = {
      id: id,
      name: name,
      addr: addr,
      sub: sub,
      enabled: enabled
    };
    nameStr = name;
    if (sub) {
      nameStr += " (" + sub + ")";
    }
    ctx.tr = $("<tr>\n    <td class=\"check\"><input type=checkbox title=\"Double click to select only this context\" data-id=\"" + id + "\" " + (enabled ? 'checked' : '') + " /></td>\n    <td class=\"addr\">[" + (formatAddr(addr, sub)) + "]</td>\n    <td class=\"name\"><span class=\"name_s\" data-id=\"" + id + "\" title=\"Click to open context log\">" + nameStr + "</span></td>\n    <td class=\"text\"><div class=\"text_s\" title=\"Click to translate last sentence\"></div></td>\n</tr>");
    contexts[id] = ctx;
    $('#contexts').append(ctx.tr);
  };

  newSentence = window.newSentence = function(id, text) {
    var addr, ctx, ctxData, enabled, lastEnabled, name, sub;
    ctx = contexts[id];
    if (!(ctx != null)) {
      ctxData = host().getContext(id);
      if (ctxData != null) {
        id = ctxData.id, name = ctxData.name, addr = ctxData.addr, sub = ctxData.sub, enabled = ctxData.enabled;
        newContext(id, name, addr, sub, enabled);
        ctx = contexts[id];
      }
    }
    if (ctx != null) {
      ctx.tr.find('.text_s').html(_.escape(text));
      if (ctx.enabled) {
        $('#contexts').prepend(ctx.tr);
      } else {
        lastEnabled = $('.check :checked:last').closest('tr');
        if (!lastEnabled.length) {
          $('#contexts').prepend(ctx.tr);
        } else {
          lastEnabled.after(ctx.tr);
        }
      }
    }
  };

  disableContexts = window.disableContexts = function(idsJson) {
    var ctx, id, ids, _i, _len;
    ids = JSON.parse(idsJson);
    for (_i = 0, _len = ids.length; _i < _len; _i++) {
      id = ids[_i];
      ctx = contexts[id];
      if (ctx != null) {
        ctx.enabled = false;
        ctx.tr.find('.check input').prop('checked', false);
      }
    }
  };

}).call(this);
