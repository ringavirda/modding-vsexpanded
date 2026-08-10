# ModDB — the site source and its public API

`.compat/vintagestory/vsmoddb` is the source of mods.vintagestory.at. It is the authority on what the
public API returns, which is how this repo checks its own published releases, download counts and
player comments without a browser.


# `.compat/vintagestory/vsmoddb` — ModDB (mods.vintagestory.at)

PHP 8, no framework, no composer. ADOdb (vendored) over MySQL/MariaDB. One front controller
(`index.php`) + one flat file per page + a hand-rolled template engine (`lib/View.php`).
Vendored HEAD is `f73e881` (~2026-05). **Production is newer than this checkout** — see gotchas.

## 1. Root directory map

| Path | What it is |
|---|---|
| `index.php` | **The only entry point.** nginx rewrites everything to it (`docker/moddb.conf:33`). Routes `/api/**` before anything else (`index.php:29-39`), then a fixed switch of page prefixes (`:54-100`), then mod url-aliases (`:95`). 404 via `showErrorPage` (`:102`). |
| `home.php` | Logged-in dashboard (own mods, followed mods). |
| `list-mod.php` | `/list/mod` — the mod browser. Also serves the infinite-scroll page fragments when `?paging` is set (`:5-19`), returning HTML + an `X-Fetch-Cursor` header. |
| `list-tag.php`, `list-user.php`, `list-sponsorable.php` | Admin/moderator-only listings. |
| `show-mod.php` | `/show/mod/{assetId}` — the mod page. Builds comments tree (`:103-171`), releases (`:173-201`), recommendations (`:274`). |
| `show-user.php` | `/show/user/{userHash}` — hex user hash, not userId. |
| `edit-mod.php`, `edit-release.php`, `edit-tag.php`, `edit-profile.php` | Form pages (HTML, session-auth). |
| `edit-uploadfile.php`, `edit-deletefile.php` | JSON-returning AJAX endpoints for the edit forms (**not** under `/api`). |
| `download.php` | `/download/{fileId}[/{name}]` — counts a download then 302s to the CDN. Also accepts legacy `?fileid=`. 410 if the release is retracted (`download.php:17`). |
| `login.php` / `logout.php` | SSO against `account.vintagestory.at` / `auth.vintagestory.at`. |
| `notifications.php`, `accountsettings.php`, `moderate-user.php`, `terms.php` | Pages. |
| `updateversiontags.php` | Pulls `http://api.vintagestory.at/stable-unstable.json` and inserts new rows into `gameVersions`. |
| `cmd-updatetrending.php` | Cron. `trendingPoints = downloads(72h) + 5 * comments(72h)` (`:32`). |
| `cmd-fixmodversionscached.php` | Cron. Rebuilds the version caches for every mod. |
| `db/` | `000_tables.sql` = **current** schema baseline (camelCase, post-rename). `100..134_*.sql|.php` = migration history (older names are snake/lowercase — ignore them). `999_sampledata.sql`, `model.mwb` (MySQL Workbench). |
| `lib/` | Everything shared. See §2. |
| `templates/*.tpl` | View templates for the pages above. |
| `web/` | `_sass/`, `_ts/` (sources), `css/`, `js/` (built), `img/`, `favicon/`, `schema/` (`modinfo.v2.rc3.json`, `worldconfig.v1.dev1.json` — the JSON-Schemas the game/editors use). |
| `util/` | `modpeek.dll` + deps (Mono.Cecil, Roslyn, VintagestoryAPI.dll). Invoked out-of-process to read `modinfo.json` out of an uploaded zip/dll. |
| `tests/` | phpunit.phar + `api-v1.php` (response-shape assertions), `version-recommendations.php`, `TrimHtmlTest.php`, `prelude.php`. |
| `docker/`, `.devcontainer/` | Local stack (nginx `moddb.conf`, php-fpm, mysql, adminer). |

## 2. `lib/` map

| Path | Purpose |
|---|---|
| `lib/config.php` | Env split on `SERVER_NAME`. Prod: `CDN=bunny`, `DOWNLOAD_DEDUPLICATION_TIMESPAN=24*3600`, `MOD_SEARCH_PAGE_SIZE=200`. `DISABLE_USER_TAGS=true` by default (`:62`). |
| `lib/core.php` | 1035 lines of everything: DB connect (`:258`), HTML sanitising (`sanitizeHtml` `:184`, `trimHtml` `:637`, `postprocessCommentHtml` `:611`, `inflateLinks` `:714`), `formatDownloadTrackingUrl` (`:875`), all `HTTP_*` constants (`:901-912`), `CATEGORY_*` (`:1005-1009`), `ASSETTYPE_*` (`:1011`), `STATUS_*` (`:1014-1017`), `TAG_KIND_*` (`:998`), `MODACTION_KIND_*` (`:414`), `logAssetChanges` (`:386`). Includes `lib/user.php` and the CDN driver at the bottom (`:857-865`). |
| `lib/user.php` | **Session auth** (`:3`, `:20-22`), `NOTIFICATION_*` kinds (`:25-38`), `canEditAsset` (`:101`), `canModerate` (`:158`), `ROLE_*` (`:334-337`), `validateActionToken` (`:326`). |
| `lib/version.php` | The 64-bit version encoding. `compileSemanticVersion` (`:20`), `compilePrimaryVersion` (`:46`), `formatSemanticVersion` (`:58`), `isPreReleaseVersion` (`:77`), `VERSION_MASK_*` (`:82-89`). |
| `lib/modinfo.php` | `modpeek()` subprocess wrapper (`:13`), `deserializeModInfoArrayFields` (`:95`), `findMinCompatibleGameVersion` (`:123`). |
| `lib/edit-release.php` | `createNewRelease` (`:11`), `updateRelease` (`:65`), **`updateGameVersionsCached`** (`:139`) — rebuilds both denormalised caches. |
| `lib/search-mods.php` | The *site* search (not the API). `VALID_ORDER_BY_COLUMNS` (`:3`), `validateModSearchInputs` (`:30`), `queryModSearch` (`:198`), keyset `getNextFetchCursor` (`:413`). |
| `lib/recommend-release.php` | `selectDesiredVersions` (`:55`), `recommendReleases` (`:107`) — the "Recommended / For testers / Latest outdated" logic on the mod page. Big spec comment at `:3-44`. |
| `lib/fileupload.php` | Upload → CDN → `files` row → `modPeekResults` row (`:147-166`). |
| `lib/cdn/bunny.php` / `lib/cdn/none.php` | `formatCdnUrl`, `formatCdnUrlFromCdnPath`, `formatCdnDownloadUrl`, `uploadToCdn`, `deleteFromCdn`. Bunny download url = `{assetserver}/{cdnPath}?dl={name}` (`bunny.php:170`). |
| `lib/img.php`, `lib/file.php`, `lib/upload-limits.php`, `lib/csp.php`, `lib/View.php`, `lib/ErrorHandler.php`, `lib/notification.php`, `lib/timezones.php`, `lib/webhook-handlers.php` | Support. `UPLOAD_LIMITS` at `upload-limits.php:43` (release: 1 file, 40 MB, `dll|zip|cs`). |
| `lib/3rdparty/` | ADOdb 5, htmLawed. Never read these. |

## 3. API routing chain

```
index.php:29   urlparts[0] === 'api'  ->  shift
index.php:31     urlparts[0] === 'v2' ->  shift ; include lib/api/v2.php
index.php:36     else                 ->  include lib/api/v1/entry.php
index.php:38   exit()
```

* **v1** — `lib/api/v1/entry.php` sets `Content-Type: application/json`, defines `fail($code)` / `good($data,$code)`, then includes `functions.php` + `logic.php`. `logic.php` is one `switch($urlparts[0])`.
* **v2** — `lib/api/v2.php` defines `fail($code,$data)` (**does** set the real HTTP status), `good($data,$flags)`, `validateMethod`, `validateContentType`, the readonly gate (`:38-48`), then includes `public/_routing.php` then `authenticated/_routing.php`, then `fail(404)`.
* `public/_routing.php` dispatches `tags` / `users` / `mods`. If a `mods` sub-handler doesn't terminate, `$urlparts` is restored and execution **falls through to the authenticated router** (`public/_routing.php:15-19`).
* `authenticated/_routing.php:3-5` — `if(empty($user)) fail(401)`. Then `notifications` / `comments` / `mods` / `game-versions`.

---

## 4. API v1 — `https://mods.vintagestory.at/api/…`

Read-only, no auth, no pagination. **Every v1 response is HTTP 200**, even errors; the real code
is the `statuscode` string field in the body.

### `GET /api/tags` — `v1/logic.php:10`
```json
{"statuscode":"200","tags":[{"tagid":"467","name":"Absolute Cinema","color":"#92C96AFF"}]}
```
`tagid` is a **string**; `color` is `#RRGGBBAA` (8 hex digits, alpha last).

### `GET /api/gameversions` — `v1/logic.php:15`
```json
{"statuscode":"200","gameversions":[{"tagid":-281492156858370,"name":"1.4.4-dev.2","color":"#CCCCCC"}]}
```
`tagid` is the **negated 64-bit compiled version** (legacy shim). Ascending by version.

### `GET /api/authors[?name=…]` — `v1/logic.php:40`
```json
{"statuscode":"200","authors":[{"userid":52736,"name":"fallenstar"}]}
```
`name` does a `LIKE %…%` (first 20 chars), `LIMIT 10`, banned users excluded. **Without `name` it
dumps every user in the DB** (the test at `tests/api-v1.php:201` has to raise `memory_limit` to 10 GB).

### `GET /api/comments[/{assetId}]` — `v1/logic.php:55`
```json
{"statuscode":"200","comments":[{"commentid":215107,"assetid":53606,"userid":105404,
  "text":"<p>…</p>","created":"2026-08-10 01:22:05","lastmodified":"2026-08-10 01:22:05"}]}
```
* Path arg is the **assetId, not the modId**. Get it from `/api/mod/{x}` → `mod.assetid`.
* Without it: latest **100 comments site-wide**, ordered `lastModified DESC`.
* With it: **all** comments for that asset, no limit, still `lastModified DESC`.
* `text` is sanitised HTML (htmLawed, safe subset + youtube iframes). Deleted comments excluded.
* There is **no** username in the payload — join `userid` against `/api/authors` yourself.

### `GET /api/mods` — `v1/logic.php:29` → `v1/functions.php:148`
Returns **all published mods in one document** (7 990 mods / 3.5 MB / ~1 s as of 2026-08-10).

Query params (all optional):

| Param | Form | Handling |
|---|---|---|
| `text` | string | `asset.name LIKE %…% OR asset.text LIKE %…%` |
| `tagids[]` | **array** | AND-ed; one `EXISTS(modTags …)` per value |
| `author` | int userId | exact |
| `gameversion` | `"1.22"` or `-<int>` | matches `modCompatibleMajorGameVersionsCached` (major.minor only) |
| `gv` | `"1.22.6"` or `-<int>` | single exact version |
| `gameversions[]` | **array** | OR-ed exact versions (ignored if `gv` present) |
| `orderby` | one of `asset.created` (default) / `lastreleased` / `downloads` / `follows` / `comments` / `trendingpoints` | literal strings, whitelist at `functions.php:156` |
| `orderdirection` | `asc` / `desc` (default) | anything not `asc` ⇒ `desc` |

Element shape (`functions.php:239-256`):
```json
{"modid":9254,"assetid":53606,"downloads":12147,"follows":525,"trendingpoints":4,
 "comments":191,"name":"Steelmaking Expanded","summary":"…",
 "modidstrs":["smex"],"author":"fallenstar","urlalias":"smex","side":"both","type":"mod",
 "logo":"https://moddbcdn.vintagestory.at/….jpg","tags":["Metal","Technology"],
 "lastreleased":"2026-08-09 23:16:11"}
```
`side` ∈ `client|server|both|null`; `type` ∈ `mod|externaltool|other` (server tweaks report `mod`,
`functions.php:266-276`). `logo` may be `null`. Dates are raw MySQL `DATETIME` strings, server TZ.

### `GET /api/mod/{modId|modIdStr}` — `v1/logic.php:33` → `v1/functions.php:3`
A non-numeric arg is resolved via `modReleases.identifier` of any non-retracted release
(`functions.php:8-13`). Only `assets.statusId = 2` (published) mods resolve; otherwise `404`.

```json
{"statuscode":"200","mod":{
  "modid":9254,"assetid":53606,"name":"…","text":"<full html description>","author":"fallenstar",
  "urlalias":"smex","logofilename":"…","logofile":"…","logofiledb":"…",
  "homepageurl":"…","sourcecodeurl":"…","trailervideourl":"","issuetrackerurl":"…","wikiurl":"",
  "downloads":12147,"follows":525,"trendingpoints":4,"comments":191,
  "side":"both","type":"mod",
  "created":"2026-06-01 13:40:05","lastreleased":"2026-08-09 23:16:11","lastmodified":"2026-08-10 15:33:39",
  "tags":["Construction","Metal","Technology"],
  "releases":[{
    "releaseid":52369,
    "mainfile":"https://moddbcdn.vintagestory.at/smex_0.9.6_….zip?dl=smex_0.9.6.zip",
    "filename":"smex_0.9.6.zip","fileid":113595,"downloads":202,
    "tags":["1.22.0","1.22.1","…","1.22.6"],
    "modidstr":"smex","modversion":"0.9.6","created":"2026-08-09 23:16:11",
    "changelog":"<p>…</p>"}],
  "screenshots":[{"fileid":97352,"mainfile":"…","filename":"SE logo.jpg",
    "thumbnailfilename":"…_55_60.jpg","created":"2026-06-01 13:50:21"}]}}
```
* `releases` are ordered `created DESC`, **retracted releases are omitted entirely**.
* `release.tags` = compatible **game** versions (nothing to do with mod tags).
* `release.mainfile` is a **direct CDN url with `?dl=`** — it bypasses `/download/…` and therefore
  **does not increment the download counter**.
* `logofilename` is `@obsolete` and identical to `logofile` (`functions.php:111`).
* `lastmodified` bumps on every download-count write — useless as a content-change signal
  (comment at `functions.php:127-129`).

### `GET /api/updates?mods=a@1.0.0,b@2.3.4` — `v1/logic.php:95` → `functions.php:281`
The game client's update check. Every entry **must** carry `@version` or the whole call fails 400.
Returns only identifiers with a strictly newer non-retracted release:
```json
{"statuscode":"200","updates":{"smex":{"releaseid":52369,"mainfile":"…?dl=…","filename":"smex_0.9.6.zip",
 "fileid":113595,"downloads":202,"tags":["1.22.0",…],"modidstr":"smex","modversion":"0.9.6",
 "created":"2026-08-09 23:16:11"}}}
```
No `changelog` field here (unlike `/api/mod`). **Ignores game-version compatibility entirely** —
it only compares mod versions.

### `GET /api/changelogs` — `v1/logic.php:84`
Permanently retired. HTTP 200, `statuscode:"410"`, `Cache-Control: max-age=604800, immutable`,
one placeholder row explaining it's gone.

---

## 5. API v2 — `https://mods.vintagestory.at/api/v2/…`

Real HTTP status codes. Public subset = `mods/install-information`, `mods/{id}/releases…`,
`tags/by-name`, `users/by-name`. Everything else needs the session cookie.

### `GET /api/v2/mods/install-information` — `public/mods.php:18`
The launcher/one-click-install endpoint.

| Param | Meaning |
|---|---|
| `ids` | **required**, comma list of `{identifier}[@{version}]`. `@version` optional only when `gv` is given. |
| `gv` | semver game version, e.g. `1.22.6`. Enables upgrade recommendation. |
| `ignore-retractions` | truthy ⇒ still return the file for a retracted release (unless force-retracted). **Note the plural — README says singular and is wrong.** |
| `hosted-mode` | truthy ⇒ every id gets `errorCode 4031`, nothing else is queried (`:58-67`). |

```json
{"data":{
  "smex":{"recommendedUpgrade":"0.9.6","fileName":"smex_0.9.4.zip","fileUrl":"/download/102250/smex_0.9.4.zip"},
  "nope":{"errorCode":4041}}}
```
Error codes (`public/mods.php:3-9`): `4001` spec parse failed · `4002` no version and no `gv` ·
`4031` forbidden in hosted mode · `4032` cannot ignore retraction · `4041` spec not found ·
`4101` retracted · `4102` force-retracted (retracted by a moderator who isn't the owner).
`fileUrl` is a **tracked** `/download/{fileId}/{name}` path (relative).

### `GET /api/v2/mods/{modId}/releases` — `public/mods.php:244`
`?ignore-retractions=1` to include ignorable retractions. Map keyed by releaseId, `version DESC`:
```json
{"52369":{"identifier":"smex","version":"0.9.6"},
 "46656":{"identifier":"smex","version":"0.9.4"},
 "44495":{"identifier":"smex","version":"0.8.3","retractionReason":"<p>…</p>"}}
```
404 `{"error":"Mod not found or not released."}` if the mod isn't `statusId = 2`.

### `GET /api/v2/mods/{modId}/releases/{releaseId}` — `public/mods.php:307`
### `GET /api/v2/mods/{modId}/releases/latest[?identifier=…][&ignore-retractions=1]` — `public/mods.php:295`
```json
{"releaseId":52369,"identifier":"smex","version":"0.9.6",
 "compatibleGameVersions":["1.22.6","1.22.5","1.22.4","1.22.3","1.22.2","1.22.1","1.22.0"],
 "created":1786317371,
 "fileName":"smex_0.9.6.zip","fileUrl":"/download/113595/smex_0.9.6.zip"}
```
`created` is a **unix timestamp int** here (v1 uses a datetime string). `compatibleGameVersions`
descending. `retractionReason` present only when retracted; `fileName`/`fileUrl` are then omitted
unless the retraction is ignorable. `fileUrl` is `null` when the release has no attached file.

`/releases/all` and `/releases/new` are **not implemented** — `all` falls into the releaseId branch
and 400s with `{"error":"Malformed releaseId."}`.

### `GET /api/v2/tags/by-name/{search}?limit=N` — `public/tags.php:8`
### `GET /api/v2/users/by-name/{search}?limit=N[&contributors-only=1]` — `public/users.php:8`
`limit` default 10, max 200 (else 400). Exact match `UNION` `LIKE %…%`.
```json
{"4":"Technology"}                       // tagId -> name
{"1907F17CC43B88830C72":"fallenstar"}    // 20-hex user HASH -> name  (NOT userId)
```

### Authenticated (cookie `vs_websessionkey`; mutations also need `at=`)

| Endpoint | Method | Source |
|---|---|---|
| `/api/v2/game-versions` | GET (**401 without a session — known bug, README:330**), POST (admin) | `authenticated/game-versions.php:9`, `:14` |
| `/api/v2/game-versions/{version}` | DELETE (admin) | `game-versions.php:54` |
| `/api/v2/notifications` | GET → array of unread notification ids | `notifications.php:6` |
| `/api/v2/notifications/clear` | POST `ids=1,2,3` or `ids[]=` | `notifications.php:14` |
| `/api/v2/notifications/settings/followed-mods/{modId}` | POST `new=<flags>`; bit0 = notify on release | `notifications.php:33` |
| `…/followed-mods/{modId}/unfollow` | POST | `notifications.php:61` |
| `/api/v2/mods/{modId}/comments` | GET → **404 not implemented**; PUT body = comment HTML, `?response-to={commentId}` | `authenticated/mods.php:11` |
| `/api/v2/mods/{modId}/lock` | POST `reason=` (moderator) | `authenticated/mods.php:111` |
| `/api/v2/mods/{modId}/releases/upload-limit` | GET / PUT `limit=` (moderator) | `authenticated/mods.php:148` |
| `/api/v2/mods/{modId}/releases/{releaseId}/retraction` | PUT `reason=` | `authenticated/mods.php:202` |
| `/api/v2/mods/{modId}/tags` | POST `tags[]=` (**disabled**: `DISABLE_USER_TAGS=true`, 503) | `authenticated/mods.php:287` |
| `/api/v2/mods/{modId}/tags/{tagId}/vote` | PUT `vote=-1|0|1` (same 503) | `authenticated/mods.php:360` |
| `/api/v2/comments/{commentId}` | POST body = HTML (edit) / DELETE | `authenticated/comments.php:13`, `:69` |

### Non-`/api` machine endpoints
* `POST /webhooks/game-tag` — body = version string, header `X-Secret: …`. Adds a game version
  (`lib/webhook-handlers.php:9`).
* `POST /edit-uploadfile`, `POST /edit-deletefile` — JSON, session-auth, form-internal.
* `GET /list/mod?paging=1&…` — HTML fragments + `X-Fetch-Cursor` header (`list-mod.php:5-19`).

---

## 6. Data model (`db/000_tables.sql`)

**The polymorphic core.** Both a mod and a release are an `assets` row.

* `assets` (`:10`) — `assetId`, `assetTypeId` (1 = mod, 2 = release, `core.php:1011`), `statusId`
  (1 draft / 2 published / 4 locked, `core.php:1014`), `createdByUserId` (= the "author"),
  `name` (mod title; releases leave it null), `text` (mod description **or** release changelog).
* `mods` (`:192`) — `modId`, `assetId` (FK), `urlAlias`, `summary` (100 chars),
  `descriptionSearchable`, denormalised counters `downloads`/`follows`/`comments`/`trendingPoints`,
  `side` enum `client|server|both`, `category` TINYINT, `cardLogoFileId`/`embedLogoFileId`,
  `homepageUrl`/`sourceCodeUrl`/`issueTrackerUrl`/`wikiUrl`/`trailerVideoUrl`/`donateUrl`,
  `lastReleased`, `uploadLimitOverwrite`.
  `category`: 1 = game mod, 2 = external tool, 3 = other, 129 = server tweak (`GAME_MOD | 1<<7`).
* `modReleases` (`:253`) — `releaseId`, `modId`, `assetId` (UNIQUE), `identifier` (the
  `modinfo.json` modId string, nullable for tools), `version` BIGINT UNSIGNED (compiled),
  `created`. UNIQUE `(modId, identifier, version)` — **one mod may publish several identifiers**.
* `modReleaseRetractions` (`:273`) — presence = retracted. `reason` HTML, `lastModifiedBy`.
  Split out of `modReleases.retractionReason` by migration `128_migrate.sql`.
* `files` (`:72`) — `fileId`, `assetId` (nullable while "hovering" pre-attach), `assetTypeId`,
  `userId`, `downloads`, `name`, `cdnPath`, `order`. The release zip and every screenshot live here.
* `fileImageData` (`:92`) — `hasThumbnail`, `size` POINT.
* `modPeekResults` (`:102`) — **one row per uploaded release file**, the parsed `modinfo.json`:
  `modIdentifier`, `modVersion`, `type` (`Theme|Content|Code`), `side`, `requiredOnClient/Server`,
  `networkVersion`, `description`, `iconPath`, `website`, `rawAuthors`, `rawContributors`,
  **`rawDependencies`**, `errors`.
* `fileDownloadTracking` (`:297`) — `(ipAddress INET6, fileId, lastDownload)`; dedup window.
* `gameVersions` (`:321`) — `version` (compiled) + `sortIndex` (dense ascending rank).
* `modReleaseCompatibleGameVersions` (`:328`) — `(releaseId, gameVersion)`, the real m:n.
* `modCompatibleGameVersionsCached` (`:336`) / `modCompatibleMajorGameVersionsCached` (`:345`) —
  denormalised per-mod caches for search, rebuilt by `updateGameVersionsCached`
  (`lib/edit-release.php:139`). Exclude retracted releases.
* `tags` (`:178`) — `tagId`, `kind` (2 predefined / 3 user-defined), `name` UNIQUE, `color` INT.
  `modTags` (`:227`) `(modId, tagId, votes)`; `modTagVotes` (`:239`) per-user ±1.
* `comments` (`:141`) — `commentId`, **`assetId` (the mod's asset)**, `responseTo`,
  `conversationRoot`, `responseDepth`, `userId`, `text` HTML, `textShort`, `deleted`.
  Deliberately **no FK on assetId** (`:156 :NoCommentAssetFK`).
* `users` (`:29`) — `userId`, `hash` BINARY(10) (the public 20-hex id used in URLs), `uid`,
  `roleId` (1 admin / 2 moderator / 3 player / 4 player-no-comment), `actionToken` BINARY(8),
  `sessionToken` BINARY(32), `sessionValidUntil`, `bannedUntil`, `bio`.
* `changelogs` (`:164`) — audit trail of edits (`logAssetChanges`), coalesced within 5 minutes.
* `notifications` (`:307`), `userFollowedMods` (`:354`), `modTeamMembers` (`:366`),
  `moderationRecords` (`:53`), `roles` (`:286`), `status` (`:132`).

### Version encoding — `lib/version.php`
```
64 bits: [major:16][minor:16][patch:16][suffix:16]
suffix  = 0xffff                      for a plain release (sorts after all pre-releases)
        = kind<<12 | number           kind 4=dev, 8=pre, 12=rc
"1.22.5"        -> 281569466384383
"1.22" (primary)-> (1<<48)|(22<<32)   suffix 0, so it sorts before every pre-release of 1.22.x
```
`compilePrimaryVersion` returns `false` for a 3-part string, and `compileSemanticVersion` returns
`false` for a 2-part string — they are **not** interchangeable.

### Releases ↔ game versions
Compat is an explicit author-curated list, not a range. On upload, `findMinCompatibleGameVersion`
reads the `game@X` entry from the modinfo dependencies and **pre-ticks every known game version
≥ X** in the form (`edit-release.php:308-315`); the author can then tick/untick freely
(`lib/edit-release.php:36`, `:111`). Nothing enforces consistency with the shipped modinfo
afterwards. A game version only exists once it is in `gameVersions` (added by
`updateversiontags.php`, `POST /api/v2/game-versions`, or `POST /webhooks/game-tag`).

### How `modinfo.json` `dependencies` is surfaced
1. Upload → `modpeek(...)` runs `dotnet util/modpeek.dll -p <file>` and prints `Key: value` lines
   (`lib/modinfo.php:17`, parsed `:65-87`). `Dependencies: game@1.20.0, exlib@0.7.0`; an
   unversioned/`*` dependency is emitted **without** `@`.
2. Stored **verbatim** as `modPeekResults.rawDependencies` TEXT. The normalised
   `releaseFileDependencies` table exists only as a commented-out block (`db/000_tables.sql:124-130`).
3. `deserializeModInfoArrayFields` (`lib/modinfo.php:95`) turns it into `id => compiledVersion`,
   with **0 meaning "any"**. The value is a **MINIMUM**, not a pin — explicit note at
   `lib/modinfo.php:3-6`.
4. Its only functional consumer is `findMinCompatibleGameVersion` (`:123`) → the game-version
   pre-tick. `lib/fileupload.php:153` returns it to the upload AJAX as `gameversiondep`.
5. **No API endpoint of either version exposes dependencies.** If you need the dependency graph of
   a published mod you must download the zip and read `modinfo.json` yourself.

### Auth & limits
* **Auth = cookie `vs_websessionkey`** (base64 session token) matched against
  `users.sessionToken` with `sessionValidUntil > NOW()` (`lib/user.php:3`, `:20-22`).
  Issued for 14 days by `login.php:43`, which validates the token against
  `https://auth.vintagestory.at/webprofile`. There is **no API key, no bearer token, no OAuth**.
* **CSRF `at`** — hex of `users.actionToken`, GET or POST, required by every mutating v2 endpoint
  (`authenticated/_routing.php:16-20`). Rotated on each login.
* **Rate limits: none.** No 429 anywhere in the codebase, no nginx `limit_req`. The only throttles
  are (a) download de-duplication per `(fileId, ip)` for 24 h (`lib/config.php:57`,
  `download.php:24`) and (b) `DB_READONLY` mode → 503 + `Retry-After: 1800` for non-GET
  (`lib/api/v2.php:38-48`).
* **No CORS headers** are emitted anywhere — browser-side cross-origin use is impossible.
* `index.php:12-14`: a GET carrying an `Accept` header that contains neither `text/html` nor
  `application/json` and isn't `*/*` gets the plain-text body `not an image` with HTTP 200.

---

## 7. curl cookbook (verified against production 2026-08-10)

Our published mods: **smex** modId `9254` / assetId `53606`, **ppex** modId `9568` / assetId `55302`,
**exlib** modId `9564` / assetId `55292`. (`iwex`, `hpex`, `lpex` are not published.)

```bash
# --- all our mods: version, downloads, follows, comments, last release ---
curl -s "https://mods.vintagestory.at/api/mods?author=52736&orderby=downloads" \
| jq -r '.mods[] | [.modid, (.modidstrs|join(",")), .downloads, .follows, .comments, .lastreleased] | @tsv'

# --- our author id, if it ever changes ---
curl -s "https://mods.vintagestory.at/api/authors?name=fallenstar"

# --- latest published version of one mod (+ its game-version compat) ---
curl -s "https://mods.vintagestory.at/api/v2/mods/9254/releases/latest" | jq

# --- full release history, newest first ---
curl -s "https://mods.vintagestory.at/api/v2/mods/9254/releases" \
| jq -r 'to_entries[] | [.key, .value.version, (.value.retractionReason // "-")] | @tsv'

# --- per-release download counts (only v1 exposes these) ---
curl -s "https://mods.vintagestory.at/api/mod/smex" \
| jq -r '.mod.releases[] | [.modversion, .downloads, .created, (.tags|join(","))] | @tsv'

# --- are we shipping the latest? (what the game client asks) ---
curl -s "https://mods.vintagestory.at/api/updates?mods=smex@0.9.6,exlib@0.7.1,ppex@0.9.6" | jq '.updates'
# empty object == everything is current

# --- what a 1.22.6 client would install, given what it already has ---
curl -s "https://mods.vintagestory.at/api/v2/mods/install-information?ids=smex@0.9.4,exlib@0.7.0&gv=1.22.6" | jq

# --- newest player comments on smex (assetId, NOT modId) ---
curl -s "https://mods.vintagestory.at/api/comments/53606" \
| jq -r '.comments[:20][] | [.created, .userid, (.text|gsub("<[^>]*>";"")|.[0:140])] | @tsv'

# --- comment count deltas across our three mods ---
for a in 53606 55302 55292; do
  printf "%s\t%s\n" "$a" "$(curl -s "https://mods.vintagestory.at/api/comments/$a" | jq '.comments|length')"
done

# --- resolve a commenter's name (v1 gives only userid) ---
curl -s "https://mods.vintagestory.at/api/authors" | jq -r '.authors[] | select(.userid==105404) | .name'

# --- which game versions exist right now ---
curl -s "https://mods.vintagestory.at/api/gameversions" | jq -r '.gameversions[-15:][].name'

# --- competitors / prior art on a tag ---
curl -s "https://mods.vintagestory.at/api/v2/tags/by-name/Technology"      # -> {"4":"Technology"}
curl -s "https://mods.vintagestory.at/api/mods?tagids%5B%5D=4&gv=1.22.6&orderby=downloads" \
| jq -r '.mods[:20][] | [.name, .downloads] | @tsv'
```
Always send `-H 'Accept: application/json'` or nothing at all; a wrong `Accept` yields the string
`not an image`. Cache aggressively — `/api/mods` alone is 3.5 MB.

## Endpoints and data model

### `index.php (front controller / API router)`

`vsmoddb/index.php:29`

The single entry point. Decides v1 vs v2 API vs HTML page vs mod url-alias.

`/api/**` is handled before CSP, view init and mod-alias lookup, so no page code runs for API calls. `api/v2/...` -> lib/api/v2.php; anything else under `/api` -> lib/api/v1/entry.php. Lines 12-14 reject GETs whose Accept header is neither text/html, application/json nor */* with the literal body `not an image` at HTTP 200. Mod url-aliases are resolved LAST (`:94`) so a mod cannot shadow `api`, `show`, `edit`, `download`, …

### `lib/api/v1/logic.php (v1 dispatcher)`

`vsmoddb/lib/api/v1/logic.php:9`

One switch implementing the whole public read-only v1 API: tags, gameversions, mods, mod, authors, comments, changelogs, updates.

`comments` takes an ASSET id (`:59`), not a modId, and returns the latest 100 site-wide when omitted. `changelogs` is permanently 410 (`:84`). Falls through to `fail("400")` at `:114` for anything unrecognised. Every response is HTTP 200 — the code lives only in the `statuscode` body field (fail/good defined at `lib/api/v1/entry.php:8` and `:14`, neither calls http_response_code).

### `listMods() / listMod() (v1 payload builders)`

`vsmoddb/lib/api/v1/functions.php:148`

Build the `/api/mods` list and `/api/mod/{id}` detail documents.

`listMods` whitelist for `orderby` at `:156` (literal `asset.created`, `lastreleased`, `downloads`, `follows`, `comments`, `trendingpoints`). No LIMIT — returns every published mod (7 990 / 3.5 MB). `listMod` (`:3`) resolves a non-numeric arg through `modReleases.identifier`, requires `assets.statusId = 2`, drops retracted releases (`:55`), and returns `release.mainfile` as a raw `?dl=` CDN url that does NOT count as a download. `mapCategoryToType` (`:266`) collapses server tweaks into `"mod"`.

### `lib/api/v2.php (v2 kernel)`

`vsmoddb/lib/api/v2.php:7`

Defines fail/good/validateMethod/validateContentType and chains the public then authenticated routers.

`fail($code,$data)` DOES set the real HTTP status (`:10`) — unlike v1. `validateMethod` emits an `Allow:` header + 405. Readonly gate at `:38-48` answers every non-GET with 503 + `Retry-After: 1800`. Public routes are tried first, then `authenticated/_routing.php`, then a final `fail(HTTP_NOT_FOUND)`.

### `lib/api/public/mods.php (the only public v2 mod endpoints)`

`vsmoddb/lib/api/public/mods.php:17`

`mods/install-information` plus `mods/{modId}/releases[…]`.

Error-code constants at `:3-9` (4001/4002/4031/4032/4041/4101/4102). `install-information` reads `ignore-retractions` (PLURAL, `:27`) and `hosted-mode` (`:28`). The releases list uses `getAssoc` so the JSON is keyed by releaseId (`:273`, `:289` with JSON_FORCE_OBJECT). `latest` accepts `?identifier=` (`:298`). `created` is emitted as a UNIX_TIMESTAMP int (`:315`), unlike v1's datetime string. Non-GET methods deliberately fall through to the authenticated router.

### `lib/api/authenticated/_routing.php (auth gate)`

`vsmoddb/lib/api/authenticated/_routing.php:3`

401s anything that reaches it without a session, then routes notifications/comments/mods/game-versions.

`validateActionTokenAPI()` (`:16`) compares `$_REQUEST['at']` against `users.actionToken` — GET or POST both work. `validateUserNotBanned()` (`:9`). Because `game-versions` is registered here, `GET /api/v2/game-versions` needs a session even though it is pure read data (acknowledged bug, README:330).

### `lib/version.php (the version codec)`

`vsmoddb/lib/version.php:20`

Encode/decode the 64-bit sortable version integer used for both mod and game versions everywhere in the DB.

`compileSemanticVersion` regex `^(\d+)\.(\d+)\.(\d+)(?:-(dev|pre|rc)\.(\d+))?$` — returns false for anything else, including 2-part strings and `+metadata`. Non-prerelease suffix is 0xffff so releases sort after their pre-releases. `compilePrimaryVersion` (`:46`) takes ONLY `major.minor` and uses suffix 0. `formatSemanticVersion` (`:58`) is the inverse. `VERSION_MASK_*` at `:82-89`.

### `lib/modinfo.php (modinfo.json ingestion)`

`vsmoddb/lib/modinfo.php:13`

Runs util/modpeek.dll over an uploaded release file and parses its `Key: value` output; the only place modinfo data enters the system.

`modpeek()` shells out to `dotnet util/modpeek.dll -p <file>` and polls proc_get_status in a 10 ms sleep loop (`:31-41`) because PHP's proc_close exit code is unreliable. `deserializeModInfoArrayFields` (`:95`) splits `id@version` and stores 0 for "any" — the header comment at `:3-6` states dependencies are MINIMUMS, not pins. `findMinCompatibleGameVersion` (`:123`) extracts the `game@X` dependency; it is the ONLY functional use of dependencies anywhere.

### `lib/edit-release.php (release lifecycle + version caches)`

`vsmoddb/lib/edit-release.php:139`

createNewRelease / updateRelease / updateGameVersionsCached.

`updateGameVersionsCached($modId)` deletes and re-inserts both `modCompatibleGameVersionsCached` and `modCompatibleMajorGameVersionsCached`, excluding retracted releases (`:156`, `:165`). The major cache masks with `0xffffffff00000000`. Compat rows are only written when `category & CATEGORY__MASK === CATEGORY_GAME_MOD` (`:33`, `:75`) — tool/other mods have no game-version rows at all.

### `lib/user.php (session auth + permissions)`

`vsmoddb/lib/user.php:3`

Resolves $user from the `vs_websessionkey` cookie; defines roles, notification kinds and the canEdit/canModerate predicates.

`$_COOKIE['vs_websessionkey']` -> `WHERE sessionToken = FROM_BASE64(?) AND sessionValidUntil > NOW()` (`:21`). No API key mechanism exists. `ROLE_ADMIN=1 / ROLE_MODERATOR=2 / ROLE_PLAYER=3` (`:334`). `NOTIFICATION_*` kinds `:25-38`. `canEditAsset` (`:101`) also grants mod-team members with canEdit.

### `lib/core.php (shared kernel)`

`vsmoddb/lib/core.php:875`

URL/HTML/date helpers, all shared constants, DB connection.

`formatDownloadTrackingUrl($file)` = `/download/{fileId}/{urlencode(name)}` — the counted download path (`:875`). `HTTP_*` `:901-912`. `CATEGORY_GAME_MOD=1 / EXTERNAL_TOOL=2 / OTHER=3 / SERVER_TWEAK=1|(1<<7)=129`, `CATEGORY__MASK=0b01111111` (`:1003-1009`). `ASSETTYPE_MOD=1 / ASSETTYPE_RELEASE=2` (`:1011`). `STATUS_DRAFT=1 / RELEASED=2 / LOCKED=4` (`:1014`). `sanitizeHtml` (`:184`) = htmLawed safe mode, iframes restricted to youtube embeds only (`:234-246`).

### `db/000_tables.sql (current schema)`

`vsmoddb/db/000_tables.sql:10`

The authoritative, already-migrated schema. Read this, not the 100..134 migration files.

Polymorphic `assets` (`:10`) shared by mods (`:192`) and releases (`:253`). `modReleases` UNIQUE `(modId, identifier, version)` at `:265` — one mod can host several identifiers. `modReleaseRetractions` (`:273`) is presence-based. `modPeekResults.rawDependencies` (`:117`) is unparsed TEXT; the normalised dependency table is commented out at `:124-130`. `comments.assetId` has deliberately NO foreign key (`:156`).

### `lib/recommend-release.php (which release the site suggests)`

`vsmoddb/lib/recommend-release.php:107`

Picks the Recommended / For-testers / Latest-outdated releases shown on a mod page.

The full ruleset is spelled out in the comment block at `:3-44`. `selectDesiredVersions` (`:55`) caps the target to the highest STABLE game version when the user didn't search for one, so a game pre-release does not mark every mod outdated (`:86-90`). Not reachable through any API — v2 `install-information`'s `recommendedUpgrade` uses a completely separate SQL self-join (`public/mods.php:99-118`).

### `lib/search-mods.php (site search, mirrors nothing in the API)`

`vsmoddb/lib/search-mods.php:30`

The /list/mod browser's filtering, keyset pagination and relevance ranking.

`VALID_ORDER_BY_COLUMNS` (`:3`) uses DIFFERENT keys from the v1 API (`trendingPoints`, `lastReleased`, `created`, `name` vs the API's `asset.created`, `lastreleased`, …). Relevance score formula and its bit-twiddling explanation at `:217-247`. Keyset cursor via `?cursor[]=val&cursor[]=modId&cursor[]=score` (`:413`). This is the only paginated mod query in the codebase — the API has none.

### `README.md (official API docs — partly wrong)`

`vsmoddb/README.md:6`

The published API documentation.

v1 section `:29-73`, v2 section `:75-347`. Documents `ignore-retraction` where the code reads `ignore-retractions`; documents the error field as absent while live responses use `error` and this checkout uses `reason`; lists `/releases/all` and `/releases/new` as `400: Not implemented` when `all` actually 400s as "Malformed releaseId". Treat it as a starting index, verify against the source.


## Practices

**Address a mod by its `modinfo.json` identifier, not by numeric ids, wherever an endpoint accepts it — `/api/mod/{modIdStr}` and `/api/updates?mods=id@ver` and `/api/v2/mods/install-information?ids=id@ver` all key on `modReleases.identifier`.** — modId and assetId are database surrogate keys that appear nowhere in our repo; the identifier is the one string we control from `modinfo.json`. Hard-coding 9254/53606 into a script silently breaks if a mod is ever re-created, and gives no signal when it does.  
`vsmoddb/lib/api/v1/functions.php:8`

**Use `/download/{fileId}/{name}` (what `formatDownloadTrackingUrl` builds and what v2 returns as `fileUrl`) when you actually want the file; use the `?dl=` CDN url from v1 `mainfile` only when you deliberately want an untracked fetch.** — `/download/…` increments `files.downloads` and `mods.downloads` (deduped per IP per 24 h) and 410s on retracted releases. The v1 `mainfile` url points straight at Bunny and bypasses both — scripting against it silently under-reports our own download stats and happily serves a retracted build.  
`vsmoddb/download.php:19`

**Send array query params with PHP bracket syntax — `tagids[]=4&tagids[]=7`, `gameversions[]=1.22.6` — url-encoded as `%5B%5D`.** — `listMods` does `foreach ($_GET["tagids"] as …)`. A scalar `tagids=4` is not an array, PHP 8 emits a warning and skips the loop, and the endpoint returns 200 with the filter silently ignored. Verified live: `?text=Steelmaking&tagids=4` returns unfiltered results.  
`vsmoddb/lib/api/v1/functions.php:172`

**Treat v1 and v2 as two different protocols: v1 = always HTTP 200, read `body.statuscode`; v2 = real HTTP status, no `statuscode` field.** — A client that checks `response.ok` will treat every v1 404/400 as success and then explode on a missing key. A client that parses `statuscode` from v2 will always read undefined.  
`vsmoddb/lib/api/v1/entry.php:8`

**Never derive game-version compatibility from `modinfo.json` `dependencies` when reading the API — read `compatibleGameVersions` (v2) or `release.tags` (v1) instead.** — The `game@X` dependency only pre-ticks checkboxes on the upload form at publish time; the author is free to change them and nothing re-syncs afterwards. The stored compat list is the truth ModDB and the launcher use.  
`vsmoddb/edit-release.php:308`

**Cache API responses and back off on your own; do not poll.** — There is no rate limiting of any kind — no 429, no nginx limit_req — which means there is also nothing telling you when you are being abusive. `/api/mods` is a single unpaginated 3.5 MB document (7 990 mods, verified 2026-08-10) and `/api/authors` with no `name` dumps the entire user table (the project's own test raises memory_limit to 10 GB to run it).  
`vsmoddb/tests/api-v1.php:201`

**Encode versions the way `compileSemanticVersion` does when you need to sort or compare: `major<<48 | minor<<32 | patch<<16 | suffix`, with plain releases taking suffix 0xffff.** — It makes a plain integer comparison agree with SemVer precedence including `rc > pre > dev`. Any string comparison or naive tuple sort gets pre-releases wrong, and `compilePrimaryVersion` deliberately uses suffix 0 so that `1.22` sorts before `1.22.0-dev.1` — masked comparison only works with that convention.  
`vsmoddb/lib/version.php:20`

**When you need per-release download counts, use v1 `/api/mod/{id}`; v2 does not expose them at all.** — v2's release endpoints return only releaseId/identifier/version/compat/created/file. The download number per release lives solely in the v1 detail payload (`releases[].downloads`, from `files.downloads`).  
`vsmoddb/lib/api/v1/functions.php:68`

**Ask for comments by assetId, and expect raw sanitised HTML with no author name.** — `/api/comments/{x}` filters on `comments.assetId`, which is the MOD's asset, so passing a modId returns someone else's comments or nothing. The payload has `userid` only — resolving it to a name needs a second call to `/api/authors`, and the `text` is HTML (`<p>…</p>`, embedded `<img>`, spoiler divs), not plain text.  
`vsmoddb/lib/api/v1/logic.php:59`

**Read `db/000_tables.sql` for schema questions and ignore `db/1xx_migrate.*`.** — The migrations still refer to the pre-rename singular lowercase tables (`mod`, `release`, `file`, `follow`, `teammember`, `notification`) that no longer exist; `000_tables.sql` is the already-migrated current shape. Grepping the migrations for a column will hand you names that are wrong today.  
`vsmoddb/db/116_migrate.sql:20`

**Send `Accept: application/json` (or no Accept header) on every request.** — index.php short-circuits any GET whose Accept header contains neither `text/html` nor `application/json` and isn't `*/*`, returning the plain-text body `not an image` at HTTP 200. A client defaulting to `Accept: text/plain` gets a 200 with garbage and no error.  
`vsmoddb/index.php:12`

**Assume no browser can call this API.** — Not a single `Access-Control-Allow-*` header is emitted anywhere in the codebase or the nginx config, so any fetch from a page (including from an Artifact) is blocked by CORS. Server-side or CLI only.  
`vsmoddb/docker/moddb.conf:20`


## Gotchas

**Live production emits its error message under the key `error`; this vendored checkout writes `reason`. Verified 2026-08-10: `GET /api/v2/mods/99999999/releases` -> `{"error":"Mod not found or not released."}` while the source at that line says `['reason' => 'Mod not found or not released.']`. The README documents neither.**  
A client written from this source that reads `body.reason` will log `undefined` for every v2 error. Read `body.error ?? body.reason`. More broadly: the checkout (commit f73e881, ~2026-05) is behind production — verify any v2 detail against the live endpoint before relying on it.  
`vsmoddb/lib/api/public/mods.php:254`

**The retraction query parameter is `ignore-retractions` (plural) in the code, everywhere — install-information, the releases list, and releases/latest. The README documents it as `ignore-retraction` (singular) in all three places.**  
Following the official docs silently does nothing: the parameter is read with `$_GET['ignore-retractions'] ?? false`, so the misspelled one is just ignored and retracted releases stay hidden with no error.  
`vsmoddb/lib/api/public/mods.php:27`

**Truthiness of those flags is PHP `boolval`, so `ignore-retractions=false`, `hosted-mode=no` and `hosted-mode=off` all evaluate to TRUE. Only `0`, the empty string, or omitting the parameter are false.**  
`?hosted-mode=false` makes every requested id come back as `errorCode: 4031` (forbidden in hosted mode) with no downloads at all — the exact opposite of the intent.  
`vsmoddb/lib/api/public/mods.php:28`

**`GET /api/v2/mods/{modId}/releases/{releaseId}` ignores `{modId}` when looking up the release. The numeric branch OVERWRITES the where-clause (`$queryWhere = 'r.releaseId = '.$releaseId;`) instead of appending, so modId is used only for an existence check.**  
Verified live: `/api/v2/mods/9254/releases/52367` (smex's modId, exlib's releaseId) returns the exlib release with HTTP 200. Any code that trusts the path modId to scope the answer will happily attribute another mod's release to yours.  
`vsmoddb/lib/api/public/mods.php:308`

**Every v1 response is HTTP 200, including failures. `fail()`/`good()` in the v1 entry point only json-encode a `statuscode` field; neither ever calls `http_response_code()`.**  
Verified: `/api/mod/doesnotexist99` -> HTTP 200, body `{"statuscode":"404"}`. `curl -f`, `response.raise_for_status()` and `if response.ok` are all useless against v1. The 410-Gone `/api/changelogs` is also served as HTTP 200.  
`vsmoddb/lib/api/v1/entry.php:8`

**`/api/comments/{id}` takes an **assetId**, not a modId, and the two are numerically unrelated (smex is modId 9254 / assetId 53606).**  
Passing a modId does not error — `intval(...) > 0` is true, the WHERE just matches a different asset or nothing, and you get someone else's comments or an empty list with statuscode 200. Always resolve the assetId from `/api/mod/{identifier}` first.  
`vsmoddb/lib/api/v1/logic.php:59`

**`/api/mods` array filters must use bracket syntax. `tagids` and `gameversions` are iterated with `foreach`; a scalar value makes PHP 8 warn and skip the loop, so the filter is silently dropped.**  
Verified live: `?text=Steelmaking&tagids=4` returns mods that do not carry tag 4, with statuscode 200. `?tagids%5B%5D=4` returns 4 correctly filtered mods. A wrong-shaped filter is indistinguishable from a broad result set.  
`vsmoddb/lib/api/v1/functions.php:172`

**`gameversion` and `gv` parse with DIFFERENT functions: `gameversion` goes through `compilePrimaryVersion` (major.minor ONLY) and `gv`/`gameversions[]` through `compileSemanticVersion` (major.minor.patch ONLY). Each returns `false` for the other's format, and `false` is then interpolated straight into the SQL as `0`.**  
`?gameversion=1.22.6` matches nothing (no error), and `?gv=1.22` matches nothing. Both look like "there are no mods for that version". Verified: `gameversion=1.22` + text=Steelmaking -> 7 mods, `gv=1.22.6` + same text -> 5.  
`vsmoddb/lib/api/v1/functions.php:143`

**v1 `/api/mod` `release.mainfile` is a direct Bunny CDN url with `?dl=`, not the tracked `/download/…` path, and there is no dedicated field for the tracked one. v2 returns the tracked path in `fileUrl`.**  
Any tool that fetches `mainfile` (installers, mirrors, CI) never increments the download counters and, critically, is never blocked by a retraction — `download.php` is where the 410 for retracted releases lives.  
`vsmoddb/lib/api/v1/functions.php:65`

**In `install-information`, `formatDownloadTrackingUrl` is called without checking whether the release actually has an attached file, unlike the release-detail endpoint which guards with `$release['fileId'] ? … : null`.**  
A release row with no file (possible: `files.assetId` is nullable and files can be deleted) yields `"fileUrl": "/download//"` — a well-formed-looking but broken url instead of a null or an error code.  
`vsmoddb/lib/api/public/mods.php:143`

**`GET /api/v2/game-versions` is registered in the AUTHENTICATED router, so it 401s for anonymous callers even though it returns nothing but public data. The README flags this as a bug and it is still live.**  
Verified: HTTP 401 `{}`. Use the v1 `/api/gameversions` endpoint instead, remembering that its `tagid` is the NEGATED 64-bit compiled version (e.g. `-281492156858370` for `1.4.4-dev.2`).  
`vsmoddb/lib/api/authenticated/_routing.php:38`

**`mods.lastModified` is bumped by download-counter writes, not by content edits, and the API exposes it as the mod's `lastmodified`. The source calls this out and refuses to fix it for compatibility.**  
Using `lastmodified` as a change-detection or cache key means re-fetching constantly for popular mods and never noticing a description edit on a quiet one. Use `lastreleased` for release changes.  
`vsmoddb/lib/api/v1/functions.php:127`

**`modinfo.json` `dependencies` is never exposed by any endpoint of either API version. It is stored verbatim as an unparsed TEXT blob in `modPeekResults.rawDependencies`; the normalised table that would make it queryable exists only as a commented-out block in the schema.**  
There is no way to ask ModDB "what does mod X depend on" or "who depends on exlib". Building a dependency graph requires downloading every zip and reading modinfo.json yourself. The one derived signal — the minimum `game@` version — is consumed once at upload time to pre-tick checkboxes and is never stored as such.  
`vsmoddb/db/000_tables.sql:124`

**A dependency version in modinfo is a MINIMUM, never a pin, and an unversioned or `*` dependency is stored as version 0 meaning "any".**  
Bumping our declared `exlib` dependency does not stop older exlib builds from loading; it only refuses ones below the floor. Conversely, the game-version pre-tick derived from `game@X` selects EVERY known version >= X, including future ones that did not exist at upload — so a release can claim compatibility with versions it was never tested against.  
`vsmoddb/lib/modinfo.php:3`

**A mod may publish several distinct identifiers under one modId — `modReleases` is UNIQUE on `(modId, identifier, version)`, and the v1 list returns `modidstrs` as an array.**  
`/api/v2/mods/{id}/releases/latest` without `?identifier=` returns the highest VERSION across all identifiers, which can be a different mod-of-the-mod than you meant. Real examples exist in production (`laborostoolsmetalcompat` + `laborostoolsmeteoricsteeladdon` under modId 10120). Always pass `identifier` when a modId hosts more than one.  
`vsmoddb/db/000_tables.sql:265`

**In v1 payloads, GROUP_CONCAT nulls become a one-element array containing the empty string, and a null game-version list decodes to the version `"0.0.0"`.**  
A mod with no tags yields `"tags": [""]` rather than `[]`, and a release with no compatible game versions (tool/other category mods never get compat rows at all) yields `"tags": ["0.0.0"]`. Filter empty strings and treat `0.0.0` as "unspecified", not as a real version.  
`vsmoddb/lib/api/v1/functions.php:69`

**The `created`/`lastreleased` timestamp format differs between API versions: v1 returns MySQL `DATETIME` strings in the server's timezone with no offset (`"2026-08-09 23:16:11"`), v2 returns a UNIX epoch integer.**  
Parsing the v1 string as UTC skews every timestamp by the server offset, and there is nothing in the payload to detect it. Cross-referencing a v1 `created` with a v2 `created` needs an explicit conversion.  
`vsmoddb/lib/api/public/mods.php:315`

**The user-tag endpoints (`POST /mods/{id}/tags`, `PUT /mods/{id}/tags/{tagId}/vote`) are shipped but hard-disabled: `DISABLE_USER_TAGS` defaults to true and the handler answers 503 before doing anything.**  
The README documents them as working (200 on success) with no mention of the kill switch. Any tag automation will get 503 `{"reason":"User tags are currently disabled."}` forever.  
`vsmoddb/lib/config.php:62`

**There is no rate limiting anywhere — no 429 code path in PHP, no `limit_req` in the nginx config — and no CORS headers either.**  
Nothing will tell you that you are hammering the service; the only self-defence is your own backoff. And because no `Access-Control-Allow-Origin` is ever sent, the API cannot be called from any browser page, so anything client-side needs a server-side proxy.  
`vsmoddb/docker/moddb.conf:20`

**PHP fatals and uncaught exceptions on an API route render the HTML error page, not JSON — `lib/ErrorHandler.php` has no API-aware branch (no reference to `api`, `json` or `Content-Type` anywhere in it).**  
A JSON client can receive a full HTML document with an arbitrary status. Always guard `json.loads` and inspect the first byte before parsing.  
`vsmoddb/lib/api/v2.php:63`

**`/api/v2/mods/{id}/releases/all` and `/releases/new` are documented as `400: Not implemented`, but `all` actually falls into the numeric-releaseId branch and fails validation.**  
Verified live: `{"error":"Malformed releaseId."}` with HTTP 400. Anyone probing for a bulk endpoint will read that message as their own bug rather than as "this route does not exist". The real list endpoint is `/releases` with no suffix.  
`vsmoddb/lib/api/public/mods.php:310`
