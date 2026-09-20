// Injected as the first child of <head> in the Web client's served
// index.html, before Jellyfin's own bundle runs. Signs the browser in
// automatically when a trusted header is present. See
// ForwardAuthController for the server-side half of this, and
// LoginInjectionMiddleware for how this file gets served here.
(function () {
    try {
        var creds = JSON.parse(localStorage.getItem('jellyfin_credentials') || '{}');
        var servers = creds.Servers || [];
        for (var i = 0; i < servers.length; i++) {
            if (servers[i].AccessToken) {
                console.debug('[ForwardAuth] already have stored credentials, skipping');
                return;
            }
        }
    } catch (e) {
        console.error('[ForwardAuth] could not read stored credentials', e);
        return;
    }

    var deviceId = localStorage.getItem('_deviceId2');
    if (!deviceId) {
        deviceId = Array.from(crypto.getRandomValues(new Uint8Array(16)))
            .map(function (b) { return b.toString(16).padStart(2, '0'); })
            .join('');
        localStorage.setItem('_deviceId2', deviceId);
    }

    var base = window.location.pathname.replace(/web\/?(index\.html)?$/, '');
    var loginUrl = base + 'ForwardAuth/Login?deviceId=' + encodeURIComponent(deviceId);
    console.debug('[ForwardAuth] requesting session from', loginUrl);

    fetch(loginUrl, { method: 'POST' })
        .then(function (response) {
            if (!response.ok) {
                console.warn('[ForwardAuth] endpoint refused, status', response.status);
                return null;
            }
            return response.json();
        })
        .then(function (result) {
            if (!result) {
                return;
            }

            var creds;
            try {
                creds = JSON.parse(localStorage.getItem('jellyfin_credentials') || '{}');
            } catch (e) {
                creds = {};
            }
            if (!creds.Servers) {
                creds.Servers = [];
            }

            var existing = null;
            for (var i = 0; i < creds.Servers.length; i++) {
                if (creds.Servers[i].Id === result.ServerId) {
                    existing = creds.Servers[i];
                    break;
                }
            }

            // jellyfin-web joins this with a path that already starts
            // with a slash, and never strips a trailing one itself, so
            // a trailing slash here would 404 every request it makes.
            var address = (window.location.origin + base).replace(/\/+$/, '');
            if (existing) {
                existing.AccessToken = result.AccessToken;
                existing.UserId = result.User.Id;
                existing.DateLastAccessed = Date.now();
                existing.ManualAddress = address;
                existing.LastConnectionMode = 2;
            } else {
                creds.Servers.unshift({
                    Id: result.ServerId,
                    Name: 'Jellyfin',
                    AccessToken: result.AccessToken,
                    UserId: result.User.Id,
                    Type: 'Server',
                    DateLastAccessed: Date.now(),
                    LastConnectionMode: 2,
                    ManualAddress: address
                });
            }

            localStorage.setItem('jellyfin_credentials', JSON.stringify(creds));
            console.info('[ForwardAuth] signed in as', result.User.Name, '- reloading');
            // jellyfin-web is a hash router. If the browser was already
            // showing its login view, that view's hash survives a plain
            // reload and gets re-rendered regardless of the now-valid
            // session, so it has to be cleared first.
            history.replaceState(null, '', window.location.pathname + window.location.search);
            window.location.reload();
        })
        .catch(function (e) {
            console.error('[ForwardAuth] failed', e);
        });
})();
