# Jellyfin Forward Auth Plugin

A minimalistic [Jellyfin](https://jellyfin.org/) plugin for [forward auth](https://docs.goauthentik.io/add-secure-apps/providers/proxy/forward_auth/), where the user is automatically identified using headers from a trusted reverse proxy.

## Overall design

A reverse proxy, such as [Caddy](https://caddyserver.com/), can be configured to handle authentication.
Only authenticated requests, annotated with trusted headers, reach the actual service.

Jellyfin is not designed for login procedures that do not rely on its native login page.
The access token is negotiated by the client, which is then attached to requests.

This plugin is heavily inspired by [Jellyfin Security](https://github.com/ZL154/JellyfinSecurity)'s approach, which injects a small piece of JS, run before Jellyfin's own login logic.
On page load, if there is no valid session in local browser storage, the script calls a custom endpoint.
By the time the request reach the backend, the reverse proxy will have attached the additional trusted headers, which are used by the plugin to identify the user:

 * If the user already exists, and is associated to this plugin, a session is created. The script will skip the login page entirely.
 * If the user exists, but is owned by another provider, the call is rejected. The script will silently fail, and let the login page show.
 * If the user does not exist, a new one is silently created; this plugin fully trusts the reverse proxy and identifity provider.

Internally, users created by this plugin are assigned its own "Forward Auth" authentication provider, which refuses any password login.
An administrator can hand an existing user to this plugin, or release one from it, by changing the provider on the user's profile page.

On a similar note, the logout flow cannot be completed.
While the session will be properly cleared, the user will just get automatically logged in again at their next page navigation, as the identity provider was not notified.

By design, this plugin only works with client relying on the official Web interface.

## Getting started

Build and publish the plugin:

```sh
cd ForwardAuth
dotnet publish --configuration Release --output bin
```

Copy `bin/ForwardAuth.dll` into a `plugins/ForwardAuth/` folder inside the Jellyfin data directory, then restart the server.

By default, it expects a `X-authentik-username` header, as set by Authentik.
This can be changed in the plugin settings.

Optionally, an administrator group can be set: members of that Authentik group, read from the `X-authentik-groups` header, are made Jellyfin administrators at login, and lose that right once removed from the group.

Since reaching the settings already requires an administrator, the group is best set before the first start, in `plugins/configurations/ForwardAuth.xml` inside the Jellyfin data directory:

```xml
<PluginConfiguration>
  <AdminGroup>jellyfin-admins</AdminGroup>
</PluginConfiguration>
```
