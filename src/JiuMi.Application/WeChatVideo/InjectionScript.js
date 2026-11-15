(function () {
  if (window.__JIUMI_WECHAT_CAPTURE__) return;
  window.__JIUMI_WECHAT_CAPTURE__ = true;

  var CAPTURE_URL = "__JIUMI_CAPTURE_URL__";

  function isFeedPage() {
    try {
      return location.pathname === '/web/pages/feed';
    } catch (e) {
      return false;
    }
  }

  function send(payload) {
    try {
      if (!isFeedPage()) return;
      payload.kind = 'video';
      payload.page = location.href;
      payload.ts = Date.now();
      fetch(CAPTURE_URL, { method: 'POST', mode: 'no-cors', body: JSON.stringify(payload) });
    } catch (e) {
    }
  }

  function handleResult(result) {
    if (!result || typeof result.err_msg !== 'string' || result.err_msg.indexOf('H5ExtTransfer:ok') < 0) return;

    var value = {};
    try {
      var raw = result.jsapi_resp && result.jsapi_resp.resp_json;
      if (typeof raw === 'string') {
        value = JSON.parse(raw);
      } else if (raw && typeof raw === 'object') {
        value = raw;
      }
    } catch (e) {
      return;
    }

    var object = value.object || {};
    var desc = object.object_desc || {};
    var media = desc.media && desc.media.length ? desc.media[0] : null;
    if (!media) return;

    send({
      url: String(media.url || '') + String(media.url_token || ''),
      decodeKey: media.decode_key,
      videoLength: media.video_play_len,
      thumbUrl: media.thumb_url,
      fileSize: media.file_size,
      description: desc.description,
      uploader: object.nickname
    });
  }

  function wrapBridge() {
    try {
      if (!window.WeixinJSBridge || typeof window.WeixinJSBridge.invoke !== 'function') return false;
      if (window.WeixinJSBridge.__jiumiCaptureWrapped) return true;
      window.WeixinJSBridge.__jiumiCaptureWrapped = true;

      var originalInvoke = window.WeixinJSBridge.invoke;
      window.WeixinJSBridge.invoke = function () {
        if (arguments.length === 3) {
          var callback = arguments[2];
          arguments[2] = function () {
            try {
              handleResult(arguments[0]);
            } catch (e) {
            }
            return callback.apply(this, arguments);
          };
        }
        return originalInvoke.apply(this, arguments);
      };
      return true;
    } catch (e) {
      return false;
    }
  }

  if (!wrapBridge()) {
    var attempts = 0;
    var timer = setInterval(function () {
      attempts++;
      if (wrapBridge() || attempts > 75) clearInterval(timer);
    }, 200);
  }
})();
