FB.register('search', function() {
    'use strict';
    var input = document.getElementById('search-input');
    var form = document.getElementById('search-form');
    var loading = document.getElementById('search-loading');
    if (!input || !form) return;
    var timer = null;
    var currentTab = new URLSearchParams(window.location.search).get('tab') || 'all';
    input.addEventListener('input', function() {
        clearTimeout(timer);
        var q = this.value.trim();
        if (q.length < 2) return;
        loading.style.display = 'block';
        timer = setTimeout(function() {
            var url = '/search?q=' + encodeURIComponent(q) + '&tab=' + encodeURIComponent(currentTab);
            window.location.href = url;
        }, 400);
    });
    form.addEventListener('submit', function() {
        clearTimeout(timer);
        loading.style.display = 'none';
    });
});
