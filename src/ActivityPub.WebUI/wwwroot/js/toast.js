(function() {
    'use strict';

    var container = document.getElementById('toast-container');
    if (!container) return;

    var DURATION = 4000;

    window.showToast = function(message, type) {
        if (!message) return;
        type = type || 'info';

        var toast = document.createElement('div');
        toast.className = 'toast toast-' + type;
        toast.setAttribute('role', 'status');

        var iconName = type === 'success' ? 'check' : type === 'error' ? 'close' : type === 'warning' ? 'warning' : 'info';
        var icon = (window.FB && FB.icon) ? FB.icon(iconName) : iconName;
        toast.innerHTML = '<span class="toast-icon" aria-hidden="true">' + icon + '</span><span class="toast-msg"></span>';
        toast.querySelector('.toast-msg').textContent = message;

        container.appendChild(toast);

        requestAnimationFrame(function() {
            toast.classList.add('toast-visible');
        });

        var timeout = setTimeout(function() {
            dismiss(toast);
        }, DURATION);

        toast.addEventListener('click', function() {
            clearTimeout(timeout);
            dismiss(toast);
        });

        function dismiss(el) {
            el.classList.remove('toast-visible');
            el.classList.add('toast-hidden');
            setTimeout(function() {
                if (el.parentNode) el.parentNode.removeChild(el);
            }, 300);
        }
    };
})();
