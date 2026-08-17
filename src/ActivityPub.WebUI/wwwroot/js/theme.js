(function() {
    'use strict';
    try {
        var t = localStorage.getItem('fediblog-theme');
        if (t !== 'light' && t !== 'dark') {
            t = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
        }
        document.documentElement.setAttribute('data-theme', t);
    } catch (e) {
        document.documentElement.setAttribute('data-theme', 'light');
    }
})();
