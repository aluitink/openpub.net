FB.register('suggestions', function() {
    'use strict';
    var addBtn = document.getElementById('add-filter-btn');
    var input = document.getElementById('filterInput');

    if (addBtn && input) {
        addBtn.addEventListener('click', async function() {
            var keyword = input.value.trim();
            if (!keyword) return;

            await fetch('/suggestions/add-filter', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ keyword })
            });

            input.value = '';
            location.reload();
        });
    }

    document.querySelectorAll('.btn-remove-filter[data-filter]').forEach(function(btn) {
        btn.addEventListener('click', async function() {
            await fetch('/suggestions/remove-filter', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ keyword: btn.dataset.filter })
            });

            location.reload();
        });
    });
});
