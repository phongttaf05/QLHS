// Global Loading Overlay cho toàn bộ hệ thống QLHS (fetch AJAX, submit form, chuyển trang)
(function () {
    if (window.__qlhsLoadingInitialized) return;
    window.__qlhsLoadingInitialized = true;

    let pendingCount = 0;
    let overlayEl = null;

    function ensureOverlay() {
        if (overlayEl) return overlayEl;
        overlayEl = document.createElement('div');
        overlayEl.id = 'qlhsGlobalLoading';
        overlayEl.className = 'd-print-none';
        overlayEl.style.cssText =
            'position:fixed;inset:0;z-index:2000;display:none;align-items:center;justify-content:center;' +
            'background:rgba(15,23,42,0.38);backdrop-filter:blur(2px);';
        overlayEl.innerHTML =
            '<div class="bg-white rounded-4 shadow-lg px-4 py-3 d-flex align-items-center gap-3 border">' +
            '  <div class="spinner-border text-primary" role="status" style="width:1.85rem;height:1.85rem;"></div>' +
            '  <div>' +
            '    <div class="fw-bold text-dark" style="font-size:0.95rem;">Đang xử lý...</div>' +
            '    <div class="text-muted" style="font-size:0.8rem;">Vui lòng chờ trong giây lát</div>' +
            '  </div>' +
            '</div>';
        document.body.appendChild(overlayEl);
        return overlayEl;
    }

    function showLoading() {
        pendingCount++;
        const el = ensureOverlay();
        el.style.display = 'flex';
    }

    function hideLoading(force) {
        if (force) {
            pendingCount = 0;
        } else {
            pendingCount = Math.max(0, pendingCount - 1);
        }
        if (pendingCount === 0 && overlayEl) {
            overlayEl.style.display = 'none';
        }
    }

    window.showGlobalLoading = showLoading;
    window.hideGlobalLoading = hideLoading;

    // 1. Tự động bật loading cho mọi lời gọi fetch()
    const originalFetch = window.fetch;
    if (typeof originalFetch === 'function') {
        window.fetch = function () {
            showLoading();
            return originalFetch.apply(this, arguments).then(
                function (response) {
                    hideLoading();
                    return response;
                },
                function (error) {
                    hideLoading();
                    throw error;
                }
            );
        };
    }

    // 2. Tự động bật loading khi submit form thường (không bị preventDefault)
    document.addEventListener('submit', function (e) {
        const form = e.target;
        if (!form || form.hasAttribute('data-no-loading')) return;
        // Chờ các handler khác chạy xem có gọi preventDefault() hay không
        setTimeout(function () {
            if (!e.defaultPrevented) {
                showLoading();
            }
        }, 0);
    }, false);

    // 3. Tự động bật loading khi click link điều hướng sang trang khác
    document.addEventListener('click', function (e) {
        const link = e.target.closest('a[href]');
        if (!link) return;
        if (e.defaultPrevented || e.ctrlKey || e.metaKey || e.shiftKey || e.button !== 0) return;

        const href = (link.getAttribute('href') || '').trim();
        if (!href || href.startsWith('#') || href.startsWith('javascript:')) return;
        if (link.target === '_blank' || link.hasAttribute('download')) return;
        if (link.hasAttribute('data-bs-toggle') || link.hasAttribute('data-no-loading')) return;
        if (href.toLowerCase().includes('exportcsv')) return;

        showLoading();
    }, false);

    // 4. Đảm bảo ẩn loading khi quay lại trang từ cache trình duyệt (Back/Forward)
    window.addEventListener('pageshow', function () {
        hideLoading(true);
    });
})();
