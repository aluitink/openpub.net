(function() {
    'use strict';

    var FB = {
        modules: {},
        ready: false,

        register: function(name, fn) {
            this.modules[name] = fn;
            if (this.ready) this.run(name);
            return this;
        },

        run: function(name) {
            var fn = this.modules[name];
            if (!fn) return;
            delete this.modules[name];
            try {
                fn();
            } catch (err) {
                console.error('[fediblog] module "' + name + '" failed:', err);
            }
        },

        ready: function() {
            if (this.ready) return;
            this.ready = true;
            var pending = this.modules;
            this.modules = {};
            for (var name in pending) this.run(name);
        },

        decodeUrl: function(v) {
            return v ? v.replace(/&amp;/g, '&') : v;
        },

        timeAgo: function(date) {
            var diff = Date.now() - date.getTime();
            var mins = Math.floor(diff / 60000);
            if (mins < 1) return 'just now';
            if (mins < 60) return mins + 'm ago';
            var hrs = Math.floor(mins / 60);
            if (hrs < 24) return hrs + 'h ago';
            var days = Math.floor(hrs / 24);
            if (days < 30) return days + 'd ago';
            return date.toLocaleDateString();
        },

        updateTimestamps: function() {
            document.querySelectorAll('.note-timestamp[data-published]').forEach(function(el) {
                var d = new Date(el.dataset.published);
                el.textContent = FB.timeAgo(d);
            });
        }
    };
    window.FB = FB;

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function() { FB.ready(); });
    } else {
        FB.ready();
    }

    // ---- Toasts -----------------------------------------------------------
    FB.register('toast', function() {
        var container = document.getElementById('toast-container');
        if (!container) return;

        var DURATION = 4000;

        window.showToast = function(message, type) {
            if (!message) return;
            type = type || 'info';

            var toast = document.createElement('div');
            toast.className = 'toast toast-' + type;
            toast.setAttribute('role', 'status');

            var icon = type === 'success' ? '✓' : type === 'error' ? '✕' : type === 'warning' ? '⚠' : 'ℹ';
            toast.innerHTML = '<span class="toast-icon">' + icon + '</span><span class="toast-msg"></span>';
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
    });

    // ---- Theme toggle -----------------------------------------------------
    FB.register('theme', function() {
        function currentTheme() {
            return document.documentElement.getAttribute('data-theme') === 'dark' ? 'dark' : 'light';
        }
        function paint(btn) {
            if (!btn) return;
            var icon = btn.querySelector('.theme-toggle-icon');
            if (icon) icon.textContent = currentTheme() === 'dark' ? '☀' : '☾';
            btn.setAttribute('aria-pressed', currentTheme() === 'dark' ? 'true' : 'false');
            btn.setAttribute('aria-label', currentTheme() === 'dark' ? 'Switch to light mode' : 'Switch to dark mode');
        }
        var btn = document.getElementById('theme-toggle');
        paint(btn);
        if (btn) {
            btn.addEventListener('click', function() {
                var next = currentTheme() === 'dark' ? 'light' : 'dark';
                document.documentElement.setAttribute('data-theme', next);
                try { localStorage.setItem('fediblog-theme', next); } catch (e) {}
                paint(btn);
            });
        }
    });

    // ---- Global keyboard shortcuts -----------------------------------------
    FB.register('shortcuts', function() {
        document.addEventListener('keydown', function(e) {
            var tag = (document.activeElement && document.activeElement.tagName) || '';
            if (tag === 'INPUT' || tag === 'TEXTAREA' || (document.activeElement && document.activeElement.isContentEditable)) return;
            if (e.key === '/') {
                e.preventDefault();
                var s = document.getElementById('nav-search-input');
                if (s) s.focus();
            } else if (e.key === 'n') {
                e.preventDefault();
                window.location.href = '/Compose/Index';
            }
        });
    });

    // ---- SignalR notifications (single bootstrap for the whole app) --------
    FB.register('signals', function() {
        if (typeof signalR === 'undefined') return;
        var connection = new signalR.HubConnectionBuilder()
            .withUrl('/notifications/ws')
            .withAutomaticReconnect()
            .build();
        window.fbSignalR = { connection: connection };

        var notificationCount = 0;
        var badge = document.getElementById('notification-badge');
        var soundEnabled = true;
        var desktopEnabled = false;
        var maxConnections = 5;
        var notificationSound = new Audio();
        notificationSound.src = 'data:audio/wav;base64,UklGRnoGAABXQVZFZm10IBAAAAABAAEAQB8AAEAfAAABAAgAZGF0YQoGAACBhYqFbF1fdH2LkZeVe5CTm3+UlZl+eX+Yd2V9c3F6dHByeG97b3RxcnpxdHJ5bXpvdm91b3BvcG5vbG9vbW5uc25sbmxrbm1vbWxrbGttbGxsa2xrbGtsbGtsbWxsbWxsa21sbGtsbWxtbW1sbm1tbWxtbW1tbm5tbm5ucXFucm1wb25ybXNycm5sbm1ubm1sc3JybHRycmxscnJrbG5ycnRsbG1sc3JybXJscnRsbG1scnJrbG5ycnRsa21sbW5ubW5ucnRrbW5ubW5ucm5yc3Zsc3Zrbm5yd3Rrdm5zd21tc3Vvc3VvbG1yc3Vuc3Vvc3RrbW1yc3RvcXZuc3NvbW1yc3VvcnVvc3NvbW5zc3ZvcnZvc3RvbW9zc3Zuc3Zvc3RvbW9zc3dvcndvc3RvbW9zc3dvcndvc3RvbW90c3dvcndvc3RvbW90c3dvcndvc3Q=';

        fetch('/RealtimeSettings/get')
            .then(function(r) { return r.ok ? r.json() : {}; })
            .then(function(settings) {
                if (settings) {
                    soundEnabled = settings.notificationSoundEnabled !== false;
                    desktopEnabled = settings.desktopNotificationsEnabled || false;
                    maxConnections = settings.maxConnections || 5;
                }
            })
            .catch(function() {});

        function bumpBadge() {
            notificationCount++;
            if (badge) {
                badge.textContent = notificationCount;
                badge.style.display = 'inline';
            }
        }

        connection.on('NewActivity', function(data) {
            bumpBadge();
            playNotificationSound();
            sendDesktopNotification('New ' + data.Type, data.Content || '', data.ActorName);
            console.log('[SignalR] New activity:', data.Type, 'by', data.ActorName);
        });

        connection.on('NewNotification', function(data) {
            bumpBadge();
            playNotificationSound();
            sendDesktopNotification(data.Type, data.Message || '', '');
            console.log('[SignalR] Notification:', data.Type, data.Message);
        });

        connection.on('Connected', function(username) {
            console.log('[SignalR] Connected as', username);
        });

        function playNotificationSound() {
            if (!soundEnabled) return;
            try { notificationSound.play().catch(function() {}); } catch (e) {}
        }

        function sendDesktopNotification(title, body, actorName) {
            if (!desktopEnabled) return;
            if (!('Notification' in window)) return;
            if (Notification.permission === 'granted') {
                new Notification(title + (actorName ? ' by ' + actorName : ''), { body: body });
            } else if (Notification.permission !== 'denied') {
                Notification.requestPermission().then(function(permission) {
                    if (permission === 'granted') {
                        new Notification(title, { body: body });
                    }
                });
            }
        }

        connection.start().catch(function(err) {
            console.log('[SignalR] Connection failed:', err);
        });
    });

    // ---- Relative timestamps ----------------------------------------------
    FB.register('timestamps', function() {
        if (!document.querySelector('.note-timestamp[data-published]')) return;
        FB.updateTimestamps();
        setInterval(FB.updateTimestamps, 60000);
    });

    // ---- Client-side "load more" / infinite scroll -------------------------
    FB.register('loadmore', function() {
        document.querySelectorAll('.load-more-container[data-next]').forEach(function(container) {
            var btn = container.querySelector('.load-more-btn');
            if (!btn) return;
            var feed = container.closest('[data-feed]');
            var sibling = container.nextElementSibling;
            var skeleton = sibling && sibling.id === 'load-more-skeleton'
                ? sibling
                : document.getElementById('load-more-skeleton');
            var cardSel = feed ? (feed.dataset.cardSelector || '') : '';
            if (!feed || !cardSel || !skeleton) return;

            var loading = false;
            var nextUrl = FB.decodeUrl(container.getAttribute('data-next'));

            function setLoading(state) {
                loading = state;
                skeleton.classList.toggle('active', state);
                btn.disabled = state;
                btn.textContent = state ? 'Loading…' : 'Load more';
            }

            function inject(html) {
                var doc = new DOMParser().parseFromString(html, 'text/html');
                var newFeed = doc.querySelector('[data-feed]');
                if (!newFeed) return;
                var existing = new Set();
                feed.querySelectorAll(cardSel + '[data-activity-id]').forEach(function(c) {
                    existing.add(c.getAttribute('data-activity-id'));
                });
                var frag = document.createDocumentFragment();
                newFeed.querySelectorAll(cardSel).forEach(function(card) {
                    var id = card.getAttribute('data-activity-id');
                    if (id && existing.has(id)) return;
                    if (id) existing.add(id);
                    frag.appendChild(card.cloneNode(true));
                });
                if (frag.childNodes.length) feed.appendChild(frag);

                var next = doc.querySelector('.load-more-container');
                var noMore = !doc.querySelector('.load-more-btn');
                if (next && next.getAttribute('data-next')) {
                    var decodedNext = FB.decodeUrl(next.getAttribute('data-next'));
                    container.setAttribute('data-page', next.getAttribute('data-page'));
                    container.setAttribute('data-next', next.getAttribute('data-next'));
                    nextUrl = decodedNext;
                    btn.href = decodedNext;
                    if (feed.hasAttribute('data-page')) {
                        feed.setAttribute('data-page', next.getAttribute('data-page'));
                        feed.setAttribute('data-next', next.getAttribute('data-next'));
                    }
                    FB.updateTimestamps();
                } else if (noMore) {
                    container.remove();
                    if (skeleton) skeleton.remove();
                }
            }

            async function loadMore(e) {
                if (e) e.preventDefault();
                if (loading) return;
                if (!nextUrl) return;
                setLoading(true);
                try {
                    var res = await fetch(nextUrl, { credentials: 'same-origin' });
                    if (!res.ok) throw new Error('load failed');
                    inject(await res.text());
                } catch (err) {
                    showToast('Could not load more', 'error');
                } finally {
                    setLoading(false);
                }
            }

            btn.addEventListener('click', loadMore);
            if ('IntersectionObserver' in window) {
                var observer = new IntersectionObserver(function(entries) {
                    if (entries[0] && entries[0].isIntersecting) loadMore(null);
                }, { rootMargin: '600px 0px' });
                observer.observe(skeleton);
            }
        });
    });

    // ---- Note card interactions (like/boost/delete/reply/copy) -------------
    FB.register('notes', function() {
        document.querySelectorAll('.btn-like, .btn-boost').forEach(function(btn) {
            var form = btn.closest('form');
            if (!form) return;
            form.addEventListener('submit', function(e) {
                e.preventDefault();
                var text = btn.textContent;
                var parts = text.split(/\s+/);
                var count = parseInt(parts[parts.length - 1], 10) || 0;
                var isActive = btn.classList.contains('active');
                var isLike = btn.classList.contains('btn-like');
                var newCount = isActive ? count - 1 : count + 1;
                var icon = isLike ? '♥' : '↻';
                btn.textContent = icon + ' ' + newCount;
                btn.classList.toggle('active', !isActive);
                var fd = new FormData(form);
                fetch(form.action, { method: 'POST', body: fd, credentials: 'same-origin' })
                    .then(function(r) {
                        if (!r.ok) throw new Error('Failed');
                        var msg = isLike ? (isActive ? 'Unlike successful' : 'Liked') : (isActive ? 'Unboosted' : 'Boosted');
                        showToast(msg, 'success');
                    })
                    .catch(function() {
                        btn.textContent = text;
                        btn.classList.toggle('active', isActive);
                        showToast('Action failed. Please try again.', 'error');
                    });
            });
        });

        document.querySelectorAll('.btn-delete').forEach(function(btn) {
            var form = btn.closest('form');
            if (!form) return;
            form.addEventListener('submit', function(e) {
                if (!confirm('Are you sure you want to delete this post? This cannot be undone.')) {
                    e.preventDefault();
                    return;
                }
                var noteCard = btn.closest('.note-card');
                var fd = new FormData(form);
                fetch(form.action, { method: 'POST', body: fd, credentials: 'same-origin' })
                    .then(function(r) {
                        if (!r.ok) throw new Error('Failed');
                        showToast('Post deleted', 'success');
                        if (noteCard) {
                            noteCard.style.transition = 'opacity 0.3s, transform 0.3s';
                            noteCard.style.opacity = '0';
                            noteCard.style.transform = 'translateX(20px)';
                            setTimeout(function() { noteCard.remove(); }, 300);
                        }
                    })
                    .catch(function() {
                        showToast('Delete failed. Please try again.', 'error');
                    });
            });
        });

        document.querySelectorAll('.reply-form-container form').forEach(function(form) {
            form.addEventListener('submit', function(e) {
                var fd = new FormData(form);
                fetch(form.action, { method: 'POST', body: fd, credentials: 'same-origin' })
                    .then(function(r) {
                        if (!r.ok) throw new Error('Failed');
                        showToast('Reply posted', 'success');
                        var textarea = form.querySelector('textarea');
                        if (textarea) textarea.value = '';
                    })
                    .catch(function() {
                        showToast('Reply failed. Please try again.', 'error');
                    });
            });
        });

        document.querySelectorAll('.btn-reply[data-reply-target]').forEach(function(btn) {
            btn.addEventListener('click', function(e) {
                e.preventDefault();
                var target = document.getElementById(btn.dataset.replyTarget);
                if (!target) return;
                var isOpen = !target.hidden;
                document.querySelectorAll('.reply-form-container[hidden]').forEach(function(c) { c.hidden = false; });
                document.querySelectorAll('.reply-form-container').forEach(function(c) {
                    if (c !== target) c.hidden = true;
                });
                target.hidden = isOpen;
                if (!target.hidden) {
                    var textarea = target.querySelector('textarea');
                    if (textarea) textarea.focus();
                }
            });
        });

        document.querySelectorAll('.note-more-menu').forEach(function(menu) {
            var btn = menu.querySelector('.btn-more');
            var dropdown = menu.querySelector('.note-more-dropdown');
            if (!btn || !dropdown) return;

            btn.addEventListener('click', function(e) {
                e.stopPropagation();
                var isOpen = btn.getAttribute('aria-expanded') === 'true';
                closeAllMoreMenus();
                btn.setAttribute('aria-expanded', String(!isOpen));
                dropdown.classList.toggle('open', !isOpen);
            });

            dropdown.querySelectorAll('[data-action="copy-link"]').forEach(function(item) {
                item.addEventListener('click', function() {
                    var url = item.dataset.url || window.location.origin;
                    navigator.clipboard.writeText(url).then(function() {
                        showToast('Link copied to clipboard', 'success');
                    }).catch(function() {
                        showToast('Failed to copy link', 'error');
                    });
                    closeAllMoreMenus();
                });
            });

            dropdown.querySelectorAll('a').forEach(function(link) {
                link.addEventListener('click', function() {
                    closeAllMoreMenus();
                });
            });

            dropdown.querySelectorAll('form').forEach(function(form) {
                form.addEventListener('submit', function() {
                    closeAllMoreMenus();
                });
            });
        });

        function closeAllMoreMenus() {
            document.querySelectorAll('.note-more-menu').forEach(function(m) {
                var b = m.querySelector('.btn-more');
                var d = m.querySelector('.note-more-dropdown');
                if (b) b.setAttribute('aria-expanded', 'false');
                if (d) d.classList.remove('open');
            });
        }

        document.addEventListener('click', closeAllMoreMenus);

        document.querySelectorAll('.cw-toggle-btn').forEach(function(btn) {
            var noteCard = btn.closest('.note-card');
            if (noteCard && btn.getAttribute('aria-expanded') !== 'true') {
                noteCard.classList.add('cw-hidden');
            }
            btn.addEventListener('click', function(e) {
                e.stopPropagation();
                if (!noteCard) return;
                var isExpanded = btn.getAttribute('aria-expanded') === 'true';
                noteCard.classList.toggle('cw-hidden', isExpanded);
                btn.textContent = isExpanded ? 'Show' : 'Hide';
                btn.setAttribute('aria-expanded', String(!isExpanded));
            });
        });
    });

    // ---- Generic dropdowns (nav groups + note more menus) ------------------
    FB.register('dropdowns', function() {
        if (!document.querySelector('.nav-group-toggle')) {
            var anyMore = document.querySelector('.note-more-menu');
            if (!anyMore) return;
        }

        function closeAllGroups(except) {
            document.querySelectorAll('.nav-group.open').forEach(function(g) {
                if (g !== except) {
                    g.classList.remove('open');
                    var btn = g.querySelector('.nav-group-toggle');
                    if (btn) btn.setAttribute('aria-expanded', 'false');
                }
            });
        }

        function closeAllMoreMenus() {
            document.querySelectorAll('.note-more-menu').forEach(function(m) {
                var b = m.querySelector('.btn-more');
                var d = m.querySelector('.note-more-dropdown');
                if (b) b.setAttribute('aria-expanded', 'false');
                if (d) d.classList.remove('open');
            });
        }

        function focusables(container) {
            return Array.prototype.slice.call(
                container.querySelectorAll('a[href], button:not([disabled]), [tabindex]:not([tabindex="-1"])')
            ).filter(function(el) {
                return el.offsetParent !== null || el === document.activeElement;
            });
        }

        function wireGroup(btn) {
            btn.addEventListener('click', function(e) {
                e.stopPropagation();
                var group = btn.closest('.nav-group');
                if (!group) return;

                var willOpen = !group.classList.contains('open');
                closeAllGroups(group);
                group.classList.toggle('open', willOpen);
                btn.setAttribute('aria-expanded', willOpen ? 'true' : 'false');

                if (willOpen && window.matchMedia('(min-width: 769px)').matches) {
                    var first = group.querySelector('.nav-dropdown-link');
                    if (first) first.focus();
                }
            });

            btn.addEventListener('keydown', function(e) {
                if (e.key !== 'ArrowDown') return;
                e.preventDefault();
                var group = btn.closest('.nav-group');
                if (!group || !group.classList.contains('open')) {
                    btn.click();
                    return;
                }
                var first = group.querySelector('.nav-dropdown-link');
                if (first) first.focus();
            });
        }

        document.querySelectorAll('.nav-group-toggle').forEach(wireGroup);

        document.querySelectorAll('.nav-dropdown').forEach(function(dropdown) {
            dropdown.addEventListener('keydown', function(e) {
                if (e.key !== 'Tab') return;
                var items = focusables(dropdown);
                if (!items.length) return;
                var first = items[0];
                var last = items[items.length - 1];

                if (e.shiftKey && document.activeElement === first) {
                    e.preventDefault();
                    last.focus();
                } else if (!e.shiftKey && document.activeElement === last) {
                    e.preventDefault();
                    first.focus();
                }
            });
        });

        document.querySelectorAll('.note-more-menu').forEach(function(menu) {
            var btn = menu.querySelector('.btn-more');
            var dropdown = menu.querySelector('.note-more-dropdown');
            if (!btn || !dropdown) return;
            btn.addEventListener('click', function(e) {
                e.stopPropagation();
                var isOpen = btn.getAttribute('aria-expanded') === 'true';
                closeAllMoreMenus();
                btn.setAttribute('aria-expanded', String(!isOpen));
                dropdown.classList.toggle('open', !isOpen);
            });
            dropdown.addEventListener('keydown', function(e) {
                if (e.key !== 'Tab') return;
                var items = focusables(dropdown);
                if (!items.length) return;
                var first = items[0];
                var last = items[items.length - 1];
                if (e.shiftKey && document.activeElement === first) {
                    e.preventDefault();
                    last.focus();
                } else if (!e.shiftKey && document.activeElement === last) {
                    e.preventDefault();
                    first.focus();
                }
            });
        });

        document.addEventListener('keydown', function(e) {
            if (e.key === 'Escape') {
                var menu = document.getElementById('nav-menu');
                if (menu && menu.classList.contains('open')) {
                    menu.classList.remove('open');
                    document.body.classList.remove('nav-open');
                    var scrim = document.getElementById('nav-scrim');
                    if (scrim) scrim.classList.remove('visible');
                    var hamburger = document.getElementById('nav-hamburger');
                    if (hamburger) {
                        hamburger.setAttribute('aria-expanded', 'false');
                        hamburger.setAttribute('aria-label', 'Open menu');
                        hamburger.focus();
                    }
                }
                closeAllGroups();
                closeAllMoreMenus();
            }
        });

        document.addEventListener('click', function(e) {
            if (!e.target.closest('.nav-group')) closeAllGroups();
            if (!e.target.closest('.note-more-menu')) closeAllMoreMenus();
        });
    });
})();
