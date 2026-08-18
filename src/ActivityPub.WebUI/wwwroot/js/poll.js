FB.register('poll', function() {
    'use strict';
    var question = document.getElementById('poll-question');
    var duration = document.getElementById('poll-duration');
    var multiselect = document.getElementById('poll-multiselect');
    var preview = document.getElementById('poll-preview');
    var previewQuestion = document.getElementById('preview-question');
    var previewOptions = document.getElementById('preview-options');
    var previewDuration = document.getElementById('preview-duration');
    var previewMultiselect = document.getElementById('preview-multiselect');
    var optionInputs = document.querySelectorAll('.poll-option-input');
    if (!question || !preview) return;

    function durationLabel(mins) {
        if (mins < 60) return mins + ' minutes';
        var hrs = mins / 60;
        if (hrs < 24) return hrs + ' hour' + (hrs > 1 ? 's' : '');
        var days = Math.round(hrs / 24);
        return days + ' day' + (days > 1 ? 's' : '');
    }

    function updatePreview() {
        var q = question.value.trim();
        var opts = [];
        optionInputs.forEach(function(inp) {
            if (inp.value.trim()) opts.push(inp.value.trim());
        });

        if (q && opts.length >= 2) {
            previewQuestion.textContent = q;
            previewOptions.innerHTML = '';
            opts.forEach(function(o) {
                var div = document.createElement('div');
                div.className = 'poll-preview-option';
                div.innerHTML = '<div class="poll-option-bar"><div class="poll-option-fill"></div></div><span class="poll-option-label"></span>';
                div.querySelector('.poll-option-label').textContent = o;
                previewOptions.appendChild(div);
            });
            previewDuration.innerHTML = (window.FB ? FB.icon('clock') : '') + '<span class="poll-duration-text"></span>';
            previewDuration.querySelector('.poll-duration-text').textContent = ' ' + durationLabel(parseInt(duration.value, 10));
            previewMultiselect.style.display = multiselect.checked ? 'inline' : 'none';
            if (multiselect.checked) previewMultiselect.textContent = ' · Multiple choices allowed';
            preview.style.display = 'block';
        } else {
            preview.style.display = 'none';
        }
    }

    question.addEventListener('input', updatePreview);
    duration.addEventListener('change', updatePreview);
    multiselect.addEventListener('change', updatePreview);
    optionInputs.forEach(function(inp) { inp.addEventListener('input', updatePreview); });
    updatePreview();
});
