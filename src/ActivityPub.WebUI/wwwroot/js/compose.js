(function() {
    'use strict';
    var textarea = document.getElementById('Content');
    var counter = document.getElementById('charCount');
    var preview = document.getElementById('compose-preview');
    var max = 500;
    if (!textarea || !counter) return;

    function escapeHtml(s) {
        var div = document.createElement('div');
        div.textContent = s;
        return div.innerHTML;
    }

    function renderPreview(text) {
        var html = escapeHtml(text);
        html = html.replace(/(https?:\/\/[^\s<]+)/g, '<a href="$1" target="_blank" rel="noopener">$1</a>');
        html = html.replace(/(^|\s)#(\w+)/g, '$1<span class="preview-hashtag">#$2</span>');
        html = html.replace(/(^|\s)@(\w+)/g, '$1<span class="preview-mention">@$2</span>');
        html = html.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
        html = html.replace(/\*([^*]+)\*/g, '<em>$1</em>');
        html = html.replace(/\n/g, '<br>');
        return html;
    }

    function update() {
        var len = textarea.value.length;
        counter.textContent = len;
        var parent = counter.closest('.char-counter');
        if (parent) {
            parent.classList.remove('char-ok', 'char-near', 'char-over');
            if (len > max) parent.classList.add('char-over');
            else if (len > max * 0.9) parent.classList.add('char-near');
            else parent.classList.add('char-ok');
        }
        if (preview) {
            if (len > 0) {
                preview.innerHTML = renderPreview(textarea.value);
                preview.style.display = 'block';
            } else {
                preview.style.display = 'none';
            }
        }
    }
    update();
    textarea.addEventListener('input', update);
})();
