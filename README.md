# Jellyfin Forward Auth Plugin

A Jellyfin plugin that trusts identity headers set by a reverse proxy,
such as an Authentik outpost, and signs a user into the official Web
client without showing Jellyfin's own login page.

## Getting started

Build and publish the plugin:

```sh
cd ForwardAuth
dotnet publish --configuration Release --output bin
```

`dotnet publish` also copies every framework assembly the plugin
compiles against into `bin/`, since Jellyfin's own NuGet packages do
not mark them otherwise. Jellyfin's server already provides all of
these at runtime, so do not copy the whole `bin/` folder into an
install. Instead, copy only `bin/ForwardAuth.dll` into a
`plugins/ForwardAuth/` folder inside the Jellyfin data directory, then
restart the server.
