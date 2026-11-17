(function () {
  if (window.__JIUMI_WECHAT_CAPTURE__) return;
  window.__JIUMI_WECHAT_CAPTURE__ = true;

  var CAPTURE_URL = "__JIUMI_CAPTURE_URL__";
  var DISCOVER_SOURCE = 'discover';
  var DISCOVER_MATCH_SECONDS = 1.5;
  var DISCOVER_DWELL_MS = 5000;
  var DISCOVER_LIMIT = 500;

  function isFeedPage() {
    try {
      return location.pathname === '/web/pages/feed';
    } catch (e) {
      return false;
    }
  }

  function isDiscoverPage() {
    try {
      return location.pathname === '/web/pages/home';
    } catch (e) {
      return false;
    }
  }

  function send(payload) {
    try {
      var discover = payload && payload.source === DISCOVER_SOURCE;
      if (!isFeedPage() && !(isDiscoverPage() && discover)) return;
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

  var discoverFeeds = {};
  var discoverFeedCount = 0;

  function normalizeText(text) {
    return String(text == null ? '' : text).replace(/\s+/g, ' ').trim();
  }

  function isFeedObject(value) {
    return !!value && typeof value === 'object' && !Array.isArray(value) &&
      (value.id !== undefined || value.objectNonceId || value.object_nonce_id) &&
      (value.media || value.objectDesc || value.object_desc);
  }

  function summarizeFeed(feed) {
    try {
      var desc = feed.objectDesc || feed.object_desc || {};
      var media = feed.media || desc.media || [];
      if (Array.isArray(media)) media = media.length ? media[0] : null;
      if (!media || !media.url || !media.decodeKey) return null;

      return {
        id: String(feed.id == null ? '' : feed.id),
        description: normalizeText(desc.description || feed.description),
        nickname: normalizeText(
          (feed.contact && (feed.contact.nickname || feed.contact.nick_name)) ||
          feed.nickname),
        media: {
          url: String(media.url),
          urlToken: media.urlToken == null ? '' : String(media.urlToken),
          decodeKey: media.decodeKey,
          videoLength: media.videoPlayLen == null ? null : Number(media.videoPlayLen),
          thumbUrl: media.thumbUrl == null ? null : String(media.thumbUrl),
          fileSize: media.fileSize == null ? null : Number(media.fileSize)
        }
      };
    } catch (e) {
      return null;
    }
  }

  function collectFeeds(node, depth, state) {
    if (!node || typeof node !== 'object' || depth > 6 || state.count >= 60) return;
    if (Array.isArray(node)) {
      for (var i = 0; i < node.length && state.count < 60; i++) {
        collectFeeds(node[i], depth + 1, state);
      }
      return;
    }

    if (isFeedObject(node)) {
      var summary = summarizeFeed(node);
      if (summary && summary.id) {
        if (discoverFeeds[summary.id]) {
          discoverFeeds[summary.id] = summary;
        } else if (discoverFeedCount < DISCOVER_LIMIT) {
          discoverFeeds[summary.id] = summary;
          discoverFeedCount++;
        }
      }
      return;
    }

    for (var name in node) {
      if (!Object.prototype.hasOwnProperty.call(node, name)) continue;
      if (name === 'media' || name === 'objectDesc' || name === 'object_desc') continue;
      var value = node[name];
      if (value && typeof value === 'object') {
        collectFeeds(value, depth + 1, state);
      }
    }
  }

  function ingestWorkerMessage(event) {
    try {
      var data = event && event.data;
      if (typeof data === 'string') data = JSON.parse(data);
      collectFeeds(data, 0, { count: 0 });
    } catch (e) {
    }
  }

  var boundPort = null;
  function bindWorkerPort() {
    try {
      var worker = window.xweb && window.xweb.worker;
      var port = worker && worker.port;
      if (!port || port === boundPort) return;
      boundPort = port;

      if (typeof port.addEventListener === 'function') {
        port.addEventListener('message', ingestWorkerMessage);
        if (typeof port.start === 'function') {
          try {
            port.start();
          } catch (e) {
          }
        }
      } else if (typeof port.start === 'function') {
        var previous = port.onmessage;
        port.onmessage = function () {
          ingestWorkerMessage(arguments[0]);
          if (previous) return previous.apply(this, arguments);
        };
        port.start();
      }
    } catch (e) {
    }
  }

  bindWorkerPort();
  setInterval(bindWorkerPort, 500);

  var discoverCaptured = {};
  var dwellSource = '';
  var dwellMs = 0;
  var dwellDone = false;
  var dwellLastTick = 0;

  function visibleArea(video) {
    try {
      var rect = video.getBoundingClientRect();
      var width = Math.max(0, Math.min(rect.right, window.innerWidth || 0) - Math.max(rect.left, 0));
      var height = Math.max(0, Math.min(rect.bottom, window.innerHeight || 0) - Math.max(rect.top, 0));
      return width * height;
    } catch (e) {
      return 0;
    }
  }

  function currentPlayingVideo() {
    var root = document.fullscreenElement || document.webkitFullscreenElement;
    var videos;
    try {
      videos = root && root.querySelectorAll
        ? root.querySelectorAll('video')
        : document.querySelectorAll('video');
    } catch (e) {
      return null;
    }

    var best = null;
    var bestArea = -1;
    for (var i = 0; i < videos.length && i < 20; i++) {
      var video = videos[i];
      if (video.paused) continue;
      var duration = isFinite(video.duration) ? video.duration : -1;
      if (duration <= 0) continue;
      var area = visibleArea(video);
      if (area <= 0) continue;
      if (area > bestArea) {
        bestArea = area;
        best = video;
      }
    }
    return best;
  }

  function matchFeed(duration, containerText) {
    var matches = [];
    for (var id in discoverFeeds) {
      if (!Object.prototype.hasOwnProperty.call(discoverFeeds, id)) continue;
      var feed = discoverFeeds[id];
      var length = feed.media.videoLength;
      if (length !== null && isFinite(length) &&
          Math.abs(length - duration) <= DISCOVER_MATCH_SECONDS) {
        matches.push(feed);
      }
    }

    if (matches.length <= 1) return matches.length ? matches[0] : null;

    var text = normalizeText(containerText);
    if (text) {
      var filtered = [];
      for (var i = 0; i < matches.length; i++) {
        var description = matches[i].description;
        var prefix = description.slice(0, 24);
        if (prefix && (text.indexOf(prefix) >= 0 || description.indexOf(text.slice(0, 24)) >= 0)) {
          filtered.push(matches[i]);
        }
      }
      if (filtered.length === 1) return filtered[0];
    }

    return null;
  }

  function cardText(video) {
    try {
      var root = document.fullscreenElement || document.webkitFullscreenElement;
      if (root) {
        return String(root.innerText || '').slice(0, 4000);
      }
      var card = video && video.closest
        ? video.closest('[id^="flow-feed-"],[class*="object-card"],[class*="feed-card"]')
        : null;
      return card ? String(card.innerText || '').slice(0, 4000) : '';
    } catch (e) {
      return '';
    }
  }

  function captureDiscover(video) {
    try {
      if (!isDiscoverPage()) return false;
      if (!video) return false;

      var duration = isFinite(video.duration) ? video.duration : -1;
      if (duration <= 0) return false;

      var feed = matchFeed(duration, cardText(video));
      if (!feed || !feed.id) return false;
      if (discoverCaptured[feed.id]) return true;
      discoverCaptured[feed.id] = true;

      send({
        source: DISCOVER_SOURCE,
        trigger: 'dwell',
        videoId: feed.id,
        url: feed.media.url + feed.media.urlToken,
        decodeKey: feed.media.decodeKey,
        videoLength: feed.media.videoLength === null ? Math.round(duration) : feed.media.videoLength,
        thumbUrl: feed.media.thumbUrl,
        fileSize: feed.media.fileSize,
        description: feed.description,
        uploader: feed.nickname
      });
      return true;
    } catch (e) {
      return false;
    }
  }

  function dwellTick() {
    try {
      if (!isDiscoverPage()) {
        dwellSource = '';
        dwellMs = 0;
        dwellDone = false;
        dwellLastTick = 0;
        return;
      }

      var now = Date.now();
      var video = document.visibilityState === 'visible' ? currentPlayingVideo() : null;
      if (!video) {
        dwellSource = '';
        dwellMs = 0;
        dwellDone = false;
        dwellLastTick = now;
        return;
      }

      var source = String(video.currentSrc || video.src || '');
      if (source !== dwellSource) {
        dwellSource = source;
        dwellMs = 0;
        dwellDone = false;
        dwellLastTick = now;
        return;
      }

      if (dwellLastTick > 0) {
        dwellMs += Math.min(now - dwellLastTick, 1500);
      }
      dwellLastTick = now;

      if (dwellDone || dwellMs < DISCOVER_DWELL_MS) return;
      if (captureDiscover(video)) {
        dwellDone = true;
      }
    } catch (e) {
    }
  }

  setInterval(dwellTick, 1000);
})();
