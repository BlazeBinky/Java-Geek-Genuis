# Modern Web Compatibility Gate

Do not label a build "fully modern" until these checks have been run against the exact target binary. Record version, date, target OS, result, and logs for every test.

| Area | Required checks | Status |
|---|---|---|
| HTML | semantic HTML, forms, media, SVG, canvas | Not tested |
| CSS | Grid, Flexbox, custom properties, modern selectors, viewport units | Not tested |
| JavaScript | ES modules, async/await, promises, current syntax, Web APIs | Not tested |
| Network | HTTPS, certificate errors, SNI, redirects, compressed responses | Not tested |
| Security | TLS configuration, certificate validation, mixed-content blocking, CSP, cookie attributes | Not tested |
| Storage | cookies, localStorage, IndexedDB, sessionStorage | Not tested |
| Media | audio/video formats actually supported by the target libraries | Not tested |
| Performance | startup, memory peak, large pages, long-running tabs | Not tested |
| Stability | tab close/reopen, navigation failures, crash recovery | Not tested |

## Suggested smoke-test set

Run against a controlled test server first, then a small set of public sites. A single homepage loading does not prove modern web compatibility. Do not bypass TLS certificate validation to make a site appear to work.

## Reporting

For each failure, capture the URL (without private query tokens), console output, engine version, target platform, and minimal reproducible page. Fix engine/platform causes rather than relying on user-agent spoofing.
