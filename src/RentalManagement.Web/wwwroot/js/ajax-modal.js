(function () {
    var modalEl = document.getElementById('appModal');
    if (!modalEl) return;

    var modal = new bootstrap.Modal(modalEl);
    var content = document.getElementById('appModalContent');

    function openModal(url) {
        fetch(url, { credentials: 'same-origin' })
            .then(function (res) { return res.text(); })
            .then(function (html) {
                content.innerHTML = html;
                bindValidation();
                modal.show();
            });
    }

    function bindValidation() {
        var form = content.querySelector('form');
        if (form && window.jQuery && window.jQuery.validator) {
            window.jQuery(form).removeData('validator').removeData('unobtrusiveValidation');
            window.jQuery.validator.unobtrusive.parse(form);
        }
    }

    function refresh(targetSelector, url) {
        var target = document.querySelector(targetSelector);
        if (!target) return;
        fetch(url, { credentials: 'same-origin' })
            .then(function (res) { return res.text(); })
            .then(function (html) { target.innerHTML = html; });
    }

    document.addEventListener('click', function (e) {
        var trigger = e.target.closest('[data-modal-url]');
        if (trigger) {
            e.preventDefault();
            openModal(trigger.getAttribute('data-modal-url'));
        }
    });

    content.addEventListener('submit', function (e) {
        var form = e.target.closest('[data-ajax-form]');
        if (!form) return;
        e.preventDefault();

        var submitter = e.submitter;
        var formData = new FormData(form);
        if (submitter && submitter.name) {
            formData.append(submitter.name, submitter.value);
        }

        fetch(form.action, {
            method: 'POST',
            body: formData,
            credentials: 'same-origin'
        }).then(function (res) {
            if (res.status === 422) {
                return res.text().then(function (html) {
                    content.innerHTML = html;
                    bindValidation();
                });
            }

            return res.json().then(function () {
                modal.hide();
                refresh(form.getAttribute('data-refresh-target'), form.getAttribute('data-refresh-url'));
            });
        });
    });
})();
