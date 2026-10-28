# Security Policy

## Supported versions

Only the latest released version of LibOpus.Interop receives security fixes. This project is pre-1.0, so
fixes land on the current minor rather than being backported.

## Reporting a vulnerability

**Please do not open a public issue for a security problem.**

Report it privately through GitHub Security Advisories:

1. Go to the [Security tab](../../security/advisories/new) of this repository.
2. Choose **Report a vulnerability**.
3. Describe the issue, the affected version, and how to reproduce it.

You should get an acknowledgement within a few days. Once the issue is confirmed, a fix will be
prepared privately and released together with an advisory crediting you, unless you would rather
stay anonymous.

## Scope

This library hands buffers and lengths across the boundary to libopus, and decodes packets that come
from the network. Reports about memory safety in the managed layer (a length that does not match its
buffer, a handle used after it is freed) are especially welcome. Vulnerabilities in libopus itself
should go to the Opus project's security contact (see <https://opus-codec.org/contact/>); this
repository ships libopus builds, and an advisory here follows a libopus fix.
