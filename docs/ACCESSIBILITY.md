# Accessibility (WCAG 2.1 AA)

## What is checked automatically

`ISMLTS.Tests/Integration/AccessibilityTests.cs` runs on every push in CI. It renders 48 pages (anonymous, student,
lecturer and admin) and fails the build when a page breaks one of these rules, the axe rules that can be judged from
the HTML alone:

| Rule | WCAG |
| --- | --- |
| `<html lang>` and a page `<title>` | 3.1.1, 2.4.2 |
| One `<main>` landmark and an `<h1>`; headings only step down one level at a time | 1.3.1, 2.4.6 |
| Every input, select and textarea has a label | 1.3.1, 4.1.2 |
| Every link and button has a name (visible or visually-hidden text) | 2.4.4, 4.1.2 |
| Every image has `alt`; no empty table headers | 1.1.1, 1.3.1 |
| No duplicate ids; `for` and `aria-*` references point at real ids | 4.1.1 |
| No `autofocus`, no positive `tabindex`, nothing focusable inside `aria-hidden` | 2.4.3 |

Colour contrast was checked by hand for every text/background pair in `site.css` (all at least 4.5:1, most above 7:1):
muted text `#5F6873` on white 5.65:1 and on the page background 5.27:1, links `#2F5FD0` 5.72:1, navy buttons 14.69:1,
and the calendar chips between 7.61:1 and 12.63:1. New colour pairs must pass 4.5:1 (CLAUDE.md).

## Keyboard walk-through (do this before a release)

Use only Tab, Shift+Tab, Enter, Space, the arrow keys and Esc. On every page, focus must always be visible.

1. **Any page:** the first Tab shows **Skip to content**; Enter moves focus to the page content.
2. **Log in:** fill in the form, show and hide the password with the eye button, log in. With an authenticator app,
   the code page takes the code and Enter submits it.
3. **Delete something** (for example a term): the confirm dialog opens with focus on **Cancel**, Tab stays inside the
   dialog, Esc closes it and focus goes back to the delete button. Enter on the red button deletes.
4. **Menus:** the account menu and the notification bell open with Enter, arrow keys move through them, Esc closes them.
5. **Tables:** the filter box, the "Show" dropdowns and the sortable column headers all work with the keyboard.
6. **Student:** submit an assessment with a file (the file picker opens with Enter or Space), scan in with a typed
   code, add a calendar note and tick it done.
7. **Lecturer:** start a register, capture a mark in Quick Eval ("Save & next"), release marks (dialog as in step 3).
8. **Admin:** add a student, reset a password, open the audit log and use its filter.
9. **Zoom to 200%** and narrow the window to phone width: nothing is cut off and tables scroll sideways.
10. **Reduced motion:** with "Show animations" off in Windows, pages appear without fading or moving.

A screen reader check (NVDA on Windows is free) on the log-in page, a form with errors and the confirm dialog is worth
doing once per phase.
