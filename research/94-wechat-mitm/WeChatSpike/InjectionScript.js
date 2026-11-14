(function () {
  if (window.__JIUMI_SPIKE__) return;
  window.__JIUMI_SPIKE__ = true;

  var CAPTURE_URL = "__CAPTURE_URL__";
  var pageUrl = location.href;
  var pageTitle = document.title || '';
  var seenResources = {};
  var seenCommands = {};

  function short(value, max) {
    var text = String(value == null ? '' : value);
    return text.length > max ? text.slice(0, max) + '...' : text;
  }

  function send(kind, data) {
    try {
      var payload = { kind: kind, ts: Date.now(), page: pageUrl };
      if (data) {
        for (var key in data) {
          if (Object.prototype.hasOwnProperty.call(data, key)) payload[key] = data[key];
        }
      }
      fetch(CAPTURE_URL, { method: 'POST', mode: 'no-cors', body: JSON.stringify(payload) });
    } catch (e) {
    }
  }

  function looksMedia(text) {
    return /decode_key|url_token|object_desc|video_play_len|cdn_url|finder/i.test(String(text || ''));
  }

  send('page', { title: pageTitle, page: pageUrl });
  setInterval(function () {
    try {
      if (location.href !== pageUrl) {
        pageUrl = location.href;
        pageTitle = document.title || '';
        send('page', { title: pageTitle, page: pageUrl });
      }
    } catch (e) {
    }
  }, 1000);

  function wrapBridge() {
    try {
      if (!window.WeixinJSBridge || typeof window.WeixinJSBridge.invoke !== 'function') return false;
      if (window.WeixinJSBridge.__jiumiSpikeWrapped) return true;
      window.WeixinJSBridge.__jiumiSpikeWrapped = true;
      var origin = window.WeixinJSBridge.invoke;
      window.WeixinJSBridge.invoke = function () {
        var cmd = arguments[0];
        try {
          var name = short(cmd, 80);
          if (!seenCommands[name]) {
            seenCommands[name] = true;
            send('invoke', { cmd: name });
          }
        } catch (e) {
        }

        if (arguments.length === 3) {
          var callback = arguments[2];
          arguments[2] = function () {
            try {
              var arg = arguments[0];
              if (arg) {
                if (typeof arg.err_msg === 'string' && arg.err_msg.indexOf('H5ExtTransfer:ok') >= 0) {
                  var value = {};
                  try {
                    var raw = arg.jsapi_resp && arg.jsapi_resp.resp_json;
                    if (typeof raw === 'string') value = JSON.parse(raw);
                    else if (raw && typeof raw === 'object') value = raw;
                  } catch (e) {
                  }
                  var object = value.object || {};
                  var desc = object.object_desc || {};
                  var media = desc.media && desc.media.length ? desc.media[0] : null;
                  if (media) {
                    send('video', {
                      url: String(media.url || '') + String(media.url_token || ''),
                      decodekey: media.decode_key,
                      videolen: media.video_play_len,
                      thumburl: media.thumb_url,
                      size: media.file_size,
                      description: String(desc.description || '').trim(),
                      uploader: object.nickname,
                      source: 'bridge'
                    });
                  }
                } else {
                  var text = typeof arg === 'string' ? arg : JSON.stringify(arg);
                  if (looksMedia(text)) {
                    send('bridge-other', { cmd: short(cmd, 80), payload: short(text, 20000) });
                  }
                }
              }
            } catch (e) {
            }
            return callback.apply(this, arguments);
          };
        }

        return origin.apply(this, arguments);
      };
      return true;
    } catch (e) {
      return false;
    }
  }

  if (!wrapBridge()) {
    var tries = 0;
    var timer = setInterval(function () {
      tries++;
      if (wrapBridge() || tries > 75) clearInterval(timer);
    }, 200);
  }

  try {
    var originalFetch = window.fetch;
    if (originalFetch) {
      window.fetch = function (input, init) {
        var url = typeof input === 'string' ? input : (input && input.url) || '';
        var promise = originalFetch.apply(this, arguments);
        try {
          promise.then(function (response) {
            try {
              if (!/finder|channels|video|media|micromsg|weixin/i.test(url)) return;
              var contentType = (response.headers && response.headers.get('content-type')) || '';
              if (!/json|text/i.test(contentType)) return;
              response.clone().text().then(function (text) {
                if (looksMedia(text)) {
                  send('network', { url: short(url, 500), status: response.status, body: short(text, 20000) });
                }
              }).catch(function () {
              });
            } catch (e) {
            }
          }).catch(function () {
          });
        } catch (e) {
        }
        return promise;
      };
    }
  } catch (e) {
  }

  try {
    var originalOpen = XMLHttpRequest.prototype.open;
    var originalSend = XMLHttpRequest.prototype.send;
    XMLHttpRequest.prototype.open = function (method, url) {
      try {
        this.__jiumiUrl = String(url || '');
      } catch (e) {
      }
      return originalOpen.apply(this, arguments);
    };
    XMLHttpRequest.prototype.send = function () {
      var xhr = this;
      try {
        xhr.addEventListener('load', function () {
          try {
            var url = xhr.__jiumiUrl || '';
            if (!/finder|channels|video|media|micromsg|weixin/i.test(url)) return;
            var text = xhr.responseText || '';
            if (looksMedia(text)) {
              send('network', { url: short(url, 500), status: xhr.status, body: short(text, 20000) });
            }
          } catch (e) {
          }
        });
      } catch (e) {
      }
      return originalSend.apply(this, arguments);
    };
  } catch (e) {
  }

  function scanResources() {
    try {
      var entries = performance.getEntriesByType('resource') || [];
      for (var i = 0; i < entries.length; i++) {
        var url = entries[i].name || '';
        if (/finder|video|media|vweixin/i.test(url) && !seenResources[url]) {
          seenResources[url] = true;
          send('resource', { url: short(url, 500) });
        }
      }
    } catch (e) {
    }
  }

  setTimeout(scanResources, 1000);
  setInterval(scanResources, 3000);
})();