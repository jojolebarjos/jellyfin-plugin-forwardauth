# Jellyfin Forward Auth Plugin

A [Jellyfin](https://jellyfin.org/) plugin that trusts identity headers set by a reverse proxy, such as an [Authentik](https://goauthentik.io/) outpost, and signs a user into the official Web client without showing Jellyfin's own login page.

## Overall design

A reverse proxy authenticates the user and forwards every request to Jellyfin with a header naming them.
This plugin is modeled on an Authentik outpost acting as that proxy, though nothing in it is Authentik-specific.
Jellyfin itself never sees a password for that user.

The plugin patches the Web client's page with a small script.
On a fresh load, if the browser holds no valid session yet, that script calls the plugin's own endpoint.
The endpoint reads the trusted header, creates the matching Jellyfin user on first sight, and opens a real session directly through Jellyfin's own session API, skipping the password check entirely.
The script stores the resulting access token where the Web client already looks for it, then reloads, landing the user signed in without ever seeing Jellyfin's login form.
Logging out of Jellyfin does not end the identity provider's own session; that has to be done separately, directly with the provider.

One assumption underlies all of this: the reverse proxy alone is responsible for authenticating the user and for stripping any identity header a client might try to set itself.
The plugin trusts a present header completely and verifies nothing about it independently.

Three limitations follow directly from that design, not from a missing feature:
- Only the official Web client benefits. Any other client never loads the injected script, and falls back to Jellyfin's own login page, which cannot accept a password for an account this plugin manages.
  That lockout is deliberate: it stops such an account from being taken over another way.
- An absent header never invents an identity.
  Jellyfin's normal login page shows instead.
- Logging out of Jellyfin is soft for a managed account: reloading the page while the header is still valid signs the same browser straight back in.
  Ending the identity provider's own session is the only real logout.

## Getting started

Build and publish the plugin:

```sh
cd ForwardAuth
dotnet publish --configuration Release --output bin
```

`dotnet publish` also copies every framework assembly the plugin compiles against into `bin/`, since Jellyfin's own NuGet packages do not mark them otherwise.
Jellyfin's server already provides all of these at runtime, so do not copy the whole `bin/` folder into an install.
Instead, copy only `bin/ForwardAuth.dll` into a `plugins/ForwardAuth/` folder inside the Jellyfin data directory, then restart the server.

The plugin has no config file of its own to edit.
Its settings live in Jellyfin's admin dashboard, under Dashboard → Plugins → Forward Auth.
For an Authentik outpost, the default, `X-authentik-username`, already matches.
For an Authentik outpost, set the trusted username header to `X-authentik-username` and the logout redirect URL to `/outpost.goauthentik.io/sign_out`.
