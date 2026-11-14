mergeInto(LibraryManager.library, {
  D27_CopyJoinCode: function(pointer) {
    var code = UTF8ToString(pointer);
    var fallback = function() {
      var field = document.createElement('textarea');
      field.value = code;
      field.style.position = 'fixed';
      field.style.left = '-10000px';
      document.body.appendChild(field);
      field.select();
      var copied = false;
      try { copied = document.execCommand('copy'); } catch (_) {}
      field.remove();
      if (!copied) window.prompt('Beitrittscode kopieren und an deinen Partner senden:', code);
    };
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(code).catch(fallback);
    } else fallback();
  }
});
