window.loadingManager = {
    show: function () {
        const loading = document.getElementById('app-loading');
        if (loading) loading.removeAttribute('hidden');
    },
    hide: function () {
        const loading = document.getElementById('app-loading');
        if (loading) loading.setAttribute('hidden', '');
    }
};

// Tự động ẩn khi trang load xong
window.addEventListener('load', function () {
    window.loadingManager.hide();
});