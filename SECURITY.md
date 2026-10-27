# Security policy

## Reporting a vulnerability

Please don't open a public issue for security problems. Report them privately instead:

- **GitHub:** the repository's **Security** tab → **Report a vulnerability**, or
- **Email:** rosario.martone@gmail.com, with "PhotoCommunity security" in the subject.

Include what's affected (API, Web or the app), the steps to reproduce it and the impact you expect. I'll reply within
a few days and let you know when it's fixed. Please give me a reasonable time to fix it before you disclose it.

## Supported versions

Only the latest code on `main` gets fixes.

## Scope

In scope: the code in this repository (the API, the Web UI, the MAUI app and their configuration).
Out of scope: the services it runs on (Supabase, Google, Apple, GitHub), which have their own reporting programmes,
as well as denial-of-service tests and automated scans against a live deployment.
