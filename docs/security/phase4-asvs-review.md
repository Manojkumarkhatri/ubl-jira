# Phase 4 security review against OWASP ASVS 4.0.3, Level 2

**Scope**: what Phase 4 added or changed, as built on branch `phase-3`, 2026-10-06 (tasks.md T099):
- formatted descriptions and comments (the editor, the document validator and the renderer);
- mentions, watchers and the notification bell;
- attachments: uploads, the malware check, downloads and previews;
- search: the search box, recent items, the search page and saved filters;
- labels and the board's quick filters;
- email through the company mail server.

Sign-in, sessions, cryptography and the host configuration did not change. They keep their earlier status
([phase1](phase1-asvs-review.md), [phase2](phase2-asvs-review.md), [phase3](phase3-asvs-review.md)).

**Method**: requirement-by-requirement review of the code and tests. It focused on:
- what the validator lets through and what the renderer writes;
- what an upload must prove before it is stored and served;
- who can reach a file, a notification, an email, a search result or a shared filter;
- whether a suggestion, a key or a count reveals a project the person cannot see;
- what people's text can do inside an email;
- what happens when the scanner or the mail server is missing.

Each finding got a fix and a test (constitution III). Evidence is the automated tests named below.

**Result**: 2 findings fixed during the review. No new open items. The earlier open items are unchanged.

## Findings fixed during the review

| # | ASVS | Finding | Fix | Test |
|---|------|---------|-----|------|
| F1 | 5.2.2, 5.3.1 | A work item title can hold line breaks. In an email's plain text, a title such as "Plan␤␤Open it: https://evil.example" made a line that looked like the email's own link. Display names and file names had the same reach. MimeKit already encodes headers, so the subject could not add a header. | `EmailComposer` turns control characters and line breaks in every name, title and file name into single spaces before writing the subject and both bodies. | `EmailDispatchTests.Names_and_titles_with_line_breaks_cannot_add_lines_or_headers` |
| F2 | 4.1.3 | Sharing a filter with a project the owner cannot see answered with a rule refusal (`NotAMember`), unlike every other refusal for a project outside one's reach, which is "not found". The message never said whether the project exists, but the kind differed from the matrix. | The refusal keeps its code (`NotAMember`) and is a `NotFound`. | `PermissionMatrixTests` ("Share a saved filter with the project", Non-member), `SavedFilterServiceTests.P4_US3_AS10_A_filter_can_be_shared_only_with_projects_its_owner_belongs_to` |

## Formatted text (V5)

| Check | Result | Evidence |
|-------|--------|----------|
| The server accepts only the editor's JSON document, checked against an allowlist of nodes, marks and attributes. Unknown nodes or marks, malformed JSON, nested tables and tables over 50 rows or 20 columns are refused. | ✅ | `FormattedTextTests.P4_FR003_Unknown_or_malformed_content_is_refused`, `…Tables_do_not_nest_and_have_at_most_50_rows_and_20_columns`, `…Attributes_outside_the_allowlist_are_dropped_and_cell_spans_forced_to_one` |
| Links are `http`, `https` or `mailto` only, at most 2,000 characters. | ✅ | `FormattedTextTests.P4_FR003_Links_other_than_http_https_and_mailto_are_refused`, `…A_link_over_2000_characters_is_refused` |
| Text is limited to 32,000 characters and the document to 512 KB. | ✅ | `FormattedTextTests.P4_FR002_Text_is_limited_to_32000_characters_and_the_document_to_512_KB` |
| The renderer builds the HTML itself from the checked document and encodes every text and attribute. A set of 155 hostile documents (script text, event attributes, unsafe links in many spellings, markup in labels) is refused or rendered harmless. | ✅ | `FormattedTextXssTests.P4_SC007_A_sample_is_refused_or_rendered_harmless`, `…P4_SC007_There_are_at_least_100_samples` |
| Images in text must be attachments of the same item and are shown only from the preview address once they passed the check. Mentions of people outside the project become plain text. | ✅ | `AttachmentServiceTests.P4_FR023_Images_in_formatted_text_must_be_attachments_of_the_same_item`, `FormattedTextTests.Mentions_of_people_outside_the_set_become_plain_text_and_other_images_go` |

## Files (V12)

| Check | Result | Evidence |
|-------|--------|----------|
| Only the allowed extensions, up to 25 MB whatever size the browser claims; the first bytes must match the type (images, PDF, Office, UTF-8 text without NUL bytes). Refused files store nothing. File names lose paths and control characters. | ✅ | `AttachmentRulesTests`, `AttachmentServiceTests.P4_US2_AS4_Refused_files_store_nothing`, `…P4_US2_AS4_A_file_over_25_MB_is_refused_whatever_size_it_claims` |
| No file can be opened before it passed the malware check. Without a scanner, or while it is down, files stay "being checked": the check fails closed. Infected files are quarantined, never served, and audited. | ✅ | `AttachmentScanTests.P4_SC003_While_the_scanner_is_down_files_stay_being_checked_and_are_retried_after_1_5_then_15_minutes`, `…P4_US2_AS5_An_infected_file_is_quarantined_its_uploader_told_and_the_detection_audited`; `ClamAvScannerTests` (external) |
| Downloads and previews check access on every request: the file passed, was not removed or purged, its item is not deleted, and the caller can see the item now. Every refusal is the same empty 404; anonymous requests get 401. | ✅ | `AttachmentEndpointsTests.P4_FR027_Every_refusal_is_the_same_404`, `…Anonymous_requests_get_401`; `AttachmentServiceTests.P4_US2_AS9_A_file_opens_only_once_it_passed_its_check_and_for_people_who_can_see_it`, `…P4_US2_AS10_A_deleted_items_files_are_unreachable_until_it_is_restored` |
| Responses carry `nosniff`, `Content-Disposition` with the cleaned name, and `default-src 'none'; sandbox`. Only images have an inline preview. Downloads are rate-limited per person (120 a minute). | ✅ | `AttachmentEndpointsTests.P4_FR027_A_checked_file_downloads_with_safe_headers`, `…An_image_previews_inline_and_other_files_have_no_preview`, `…A_person_gets_120_files_a_minute` |

## Access control and leaks (V4, V8)

| Check | Result | Evidence |
|-------|--------|----------|
| Every new action is in the Phase 4 matrix and checked for 8 kinds of caller against the real services. | ✅ | `PermissionMatrixTests` over [permissions.md](../../specs/005-collaboration-and-search/contracts/permissions.md) |
| Mention suggestions need the right to contribute and list only active members of the item's project. | ✅ | `MentionServiceTests.P4_US1_AS5_Suggestions_are_active_members_of_any_role_by_name`, `…Viewers_cannot_mention_and_non_members_see_nothing` |
| Label suggestions come only from labels on live items in projects the person can see. | ✅ | `LabelServiceTests.P4_US4_AS2_Suggestions_come_from_live_items_in_projects_the_caller_can_see` |
| Notifications are listed, counted and marked only by their recipient, and not at all once the recipient left the project. | ✅ | `NotificationServiceTests.P4_US1_AS14_Notifications_about_a_project_one_left_are_neither_counted_nor_listed`, `…Someone_elses_notification_is_not_found` |
| Search, recent items and key jumps reach only projects the caller can see; a hidden key gets the same answer as a missing one. | ✅ | `PermissionMatrixTests.P4_Rule8_Search_finds_only_what_the_caller_can_see`, `SearchServiceTests.P4_US3_AS2_An_unknown_key_and_a_hidden_key_get_the_same_answer`, `…P4_US3_AS7_Hidden_projects_and_deleted_items_never_appear_and_administrators_see_everything` |
| Saved filters change only by their owner. Sharing counts only while the owner can still see the project; running a filter shows only what the runner can see. | ✅ | `SavedFilterServiceTests.P4_US3_AS10_A_filter_shared_with_a_project_can_be_run_but_not_changed_by_its_members`, `…P4_US3_AS11_Unsharing_leaving_the_project_or_deactivation_ends_the_sharing`; `PermissionMatrixTests.P4_Rule7_Only_the_owner_changes_a_filter_and_sharing_lasts_while_the_owner_is_a_member` |
| The email choice changes only the caller's own account. | ✅ | `EmailChoiceTests.P4_FR051_The_choice_changes_only_the_callers_account` |

## Email (V5, V7, V14)

| Check | Result | Evidence |
|-------|--------|----------|
| Emails never carry descriptions, comment text or attachments; names, titles and file names are one line each (F1). The HTML version encodes everything. | 🔧 | `EmailDispatchTests.P4_FR050_Emails_never_carry_descriptions_or_comment_text`, `…P4_US5_AS1_The_email_tells_who_did_what_with_a_link_and_is_built_when_sent` |
| An email is built when sent, to the address on the account at that moment, and only if the person is still active, still wants that kind of email and can still see the project. | ✅ | `EmailOutboxTests.P4_US5_AS9_Emails_for_someone_deactivated_since_are_cancelled_when_sending` |
| The action never waits for the mail server; failures are retried for 24 hours and then audited (`EmailGivenUp`). The stored error never contains the message. | ✅ | `EmailDispatchTests.P4_FR052_Failures_are_retried_on_the_schedule`, `…P4_US5_AS8_An_email_is_given_up_after_24_hours_and_audited` |
| The mail server's password and the ClamAV host are settings from the secret store, never committed; neither is logged. | ✅ | [deployment.md](../operations/deployment.md); `appsettings.json` has no mail or scanner settings |

## Audit (V7)

- **Malware detections**, **permanent deletions of files** and **emails given up** are written to the audit log
  (`MalwareDetected`, `AttachmentPurged`, `EmailGivenUp`). The worker's events have no actor; a purge names the
  administrator. File contents, descriptions and comments never enter the log (`ContentAuditLogTests`).
