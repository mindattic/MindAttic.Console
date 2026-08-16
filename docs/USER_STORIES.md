---
codex: 1
project: MindAttic.Launcher
code: MCO
layer: stories
status: living
updated: 2026-06-07
---

# MindAttic.Launcher — User Stories
> ✅ done (shipped & tested) · 🟡 partial · ⬜ planned · 🗑️ cut. Every ✅ cites the test.
> Personas: **Dev** (the developer running the workspace) and **Overlord** (one agent over all repos).

## Epic A — Launch agents in tabs
- **MCO-US-A1 ✅** As a Dev, I can run `mindattic host --name <Project> [--provider <Key>]` to root
  an agent in the right repo; without `--provider` it resolves to the first-listed provider (Claude),
  so I get a working session in one step even from a script. *Given a roster with the project; When I
  host it with no explicit provider; Then the project's path and the first-listed provider are
  resolved.* *(verified by `Defaults_are_returned_when_AgentProviders_is_empty`,
  `Current_is_the_first_configured_provider`.)*
- **MCO-US-A2 🟡** As a Dev, when I reference a provider that doesn't exist, the operation fails
  loudly rather than launching a default, so a typo never starts the wrong agent. *Given an unknown
  provider key; When resolving it; Then the caller gets nothing back to fall through to a loud
  error.* *(registry rejection verified by `SetModel_unknown_key_throws`; the `HostAgentCommand`
  `--provider` exit-code path itself — [MCO-LAW-1](BIBLE.md#MCO-LAW-1) — is not yet covered by an
  automated test.)*
- **MCO-US-A3 🟡** As a Dev, opening a project tab asks fresh which agent CLI to launch it with —
  Claude first, then Codex, Gemini, Kimi — every single time, so nothing about "which CLI for this
  project" is ever saved and I can open the same project with a different CLI on a whim. *Given the
  Open Project Tab menu; When I pick a project; Then a picker over all configured providers appears,
  ordered Claude/Codex/Gemini/Kimi, and the launch uses whichever one I pick without persisting it.*
  *(the ordering contract is verified by `Defaults_order_is_Claude_Codex_Gemini_Kimi`; the menu-level
  picker itself — `OpenProjectMenu.PickProvider` — is exercised only interactively, no UI test
  harness exists yet.)*
- **MCO-US-A4 ✅** As a Dev, a provider `RunCommand` with quoted/spaced arguments is split into argv
  correctly before exec. *Given a quoted command line; When split; Then quotes and escaped quotes
  are preserved.* *(verified by `Preserves_quoted_arg_with_spaces`,
  `Preserves_escaped_quote_inside_quoted_arg`, `Splits_unquoted_args`,
  `Empty_input_returns_empty_array`.)*
- **MCO-US-A5 🟡** As a Dev, I can open a project tab from the interactive menu (title + color +
  scheme via `wt`) after picking the agent CLI for this launch. *WT command-line composition is
  covered, but the menu-driven open flow itself is exercised only interactively.* *(launcher partly
  verified by `WindowsTerminalLauncherTests`; menu path unverified.)*
- **MCO-US-A6 ✅** As a Dev, I can set the model each agent CLI runs with — or revert to the CLI's
  own default — from the Settings screen, so I can switch Claude/Codex models without retyping the
  whole `RunCommand`. *Given a provider's RunCommand; When I set a model; Then the `--model` token is
  rewritten in place (or appended/removed) and persisted, leaving the static Defaults untouched.*
  *(token rewrite verified by `Set_rewrites_existing_flag_in_place`,
  `Set_appends_flag_when_absent`, `Set_blank_removes_flag_without_leaving_double_space`,
  `Set_then_Get_round_trips`; persistence by `SetModel_appends_flag_and_persists`,
  `SetModel_blank_clears_the_flag`, `SetModel_unknown_key_throws`,
  `SetModel_materializes_defaults_when_none_configured`.)*

## Epic B — Tab title & remote control
- **MCO-US-B1 ✅** As a Dev, each host tab shows whether the agent is busy, so I can read the tab
  strip at a glance. *Given a Claude/Codex working footer **or a background shell the agent spawned
  still running**; When the pinner peeks the buffer; Then it reports busy and uses the play glyph.*
  *(verified by `LooksBusy_matches_the_Claude_working_footer`,
  `LooksBusy_matches_the_Codex_working_footer`, `LooksBusy_is_false_for_an_idle_prompt`,
  `HasBackgroundShell_matches_a_running_background_shell`,
  `HasBackgroundShell_is_false_for_an_idle_prompt_with_no_shells`,
  `Compose_uses_the_play_glyph_when_busy`.)*
- **MCO-US-B2 ✅** As a Dev, I can broadcast text (e.g. `/remote-control`) to every running host tab
  for a provider via per-tab named pipes. *Given several host pipes; When I broadcast; Then only
  matching-provider pipes receive the payload.* *(verified by
  `Filters_pipes_by_provider_prefix_and_writes_payload`,
  `Reports_zero_delivered_when_no_pipes_match`.)*

## Epic C — Commit & push
- **MCO-US-C1 ✅** As a Dev, I can commit + push one or all projects, with an auto-generated message
  from `git status --porcelain` when I give none. *Given a dirty tree; When I read status; Then it
  parses adds/mods/deletes/renames/untracked and composes a capped summary.* *(verified by
  `Status_codes_classified_correctly`, `Rename_picks_up_destination_filename`,
  `Untracked_files_count_as_added`, `Empty_porcelain_is_clean`, `Quoted_paths_are_unwrapped`,
  `AutoMessage_groups_by_action`, `AutoMessage_truncates_to_summary_when_over_limit`,
  `Short_format_uses_compact_counts`.)*
- **MCO-US-C2 ✅** As a Dev, an unreadable repo (missing path / not a repo / timeout) is treated as
  "unknown", never silently "clean", so I never push past a broken status. *Given an error status;
  When summarized; Then it is not clean and surfaces the error.* *(verified by
  `Status_with_error_is_not_clean_and_short_returns_error`,
  `Status_for_missing_directory_returns_error_state`.)*

## Epic D — Backup
- **MCO-US-D1 ✅** As a Dev, backup writes to a dated folder that never collides, walking
  `<date>`, `<date>_a`, …`_z`. *Given today's folder taken; When resolving; Then it appends the next
  free letter and throws only when all 27 slots are used.* *(verified by
  `ResolveTargetFolder_uses_dated_folder_when_unused`,
  `ResolveTargetFolder_appends_underscore_a_on_collision`,
  `ResolveTargetFolder_walks_to_first_available_letter`,
  `ResolveTargetFolder_throws_when_all_27_slots_taken`.)*
- **MCO-US-D2 ✅** As a Dev, backup is reported failed if it was cancelled even when the tool's exit
  code looked successful. *Given a success code but cancelled=true; When computing OK; Then it
  reports failure.* *(verified by `ComputeOk_reports_failure_when_cancelled_even_on_a_success_code`.)*
- **MCO-US-D3 ✅** As a Dev, each project database gets a real full backup, namespaced by server so
  the same DB on two instances doesn't collide, with illegal path chars scrubbed and identifiers
  escaped. *(verified by `ResolveBackupFilePath_places_bak_under_databases_subfolder`,
  `ResolveBackupFilePath_namespaces_by_server_so_same_db_on_two_instances_does_not_collide`,
  `ResolveBackupFilePath_scrubs_path_illegal_characters_in_server_and_database`,
  `BuildBackupSql_is_a_full_copy_only_checksummed_backup_with_escaped_identifiers` + the wider
  `SqlBackupServiceTests`.)*
- **MCO-US-D4 ✅** As a Dev, the robocopy exclude lists match the original PowerShell launcher so the
  snapshot omits the same noise (bin/obj/etc.). *(verified by `Exclude_lists_match_original_PS_launcher`.)*

## Epic E — Roster & discovery
- **MCO-US-E1 ✅** As a Dev, new git repos under the workspace root are surfaced for adding, while
  registered or ignored ones are excluded. *(verified by
  `FindUnregistered_returns_only_unknown_git_repos_sorted`,
  `FindUnregistered_matches_registered_paths_with_trailing_separator`,
  `FindUnregistered_returns_empty_when_root_missing`, `FindUnregistered_tolerates_null_collections`.)*
- **MCO-US-E2 ✅** As a Dev, a tab title strips the shared `MindAttic.` prefix (case-insensitively),
  preferring an explicit alias, so the tab strip stays readable. *(verified by
  `TabTitle_strips_the_shared_MindAttic_prefix`, `TabTitle_prefers_an_explicit_alias_over_the_name`,
  `TabTitle_leaves_names_without_the_prefix_untouched`, `TabTitle_strips_the_prefix_case_insensitively`.)*
- **MCO-US-E3 ✅** As a Dev, adding a discovered project picks a tab color and writes a matching WT
  scheme idempotently (no duplicate or clobbered schemes). *(verified by
  `Insert_adds_scheme_preserving_existing_and_staying_valid_json`,
  `Insert_is_idempotent_when_scheme_already_present`, `Insert_handles_an_empty_schemes_array`,
  `Insert_ignores_a_same_named_profile_outside_the_schemes_array`,
  `Insert_returns_false_when_no_schemes_array` + `ColorPaletteTests`.)*

## Epic F — Settings persistence
- **MCO-US-F1 ✅** As a Dev, my settings round-trip through Vault and unknown top-level / per-project
  keys are preserved across a Save. *(verified by `Save_persists_settings_round_trip`,
  `Save_preserves_unknown_top_level_and_per_project_keys`,
  `Load_returns_defaults_when_vault_is_empty`.)*
- **MCO-US-F2 ✅** As a Dev, on first run with an empty Vault, my legacy `settings.json` seeds Vault
  once. *(verified by `Load_seeds_from_legacy_file_when_vault_is_empty`.)*

## Epic G — Deploy & freshness
- **MCO-US-G1 ✅** As a Dev, the in-app "Deploy All" path locates the sibling MindAttic.Deploy exe
  and composes its `all` command line, returning null cleanly when the artifact is missing. *(verified
  by `ResolveExe_returns_sibling_path_when_artifact_present`, `ResolveExe_returns_null_when_artifact_missing`,
  `ResolveExe_returns_null_for_blank_root`, `BuildDeployAllCommandLine_delegates_to_cli_all_subcommand`.)*
- **MCO-US-G2 ✅** As a Dev, the menu warns me when the running binary is older than the latest
  commit so I know to republish, flooring a same-day lag to zero and comparing across time-zone
  offsets. *(verified by `Evaluate_reports_whole_days_behind`,
  `Evaluate_returns_null_when_build_is_newer_than_head`, `Evaluate_returns_null_when_build_equals_head`,
  `Evaluate_floors_a_same_day_lag_to_zero_days`, `Evaluate_compares_across_time_zone_offsets`.)*

## Priority backlog
Dependency-ordered toward the headline goal (a frictionless single-binary workspace orchestrator):
1. **MCO-US-A5** (🟡→✅) — make the menu-driven "Open Project Tab" flow testable (extract a pure tab
   spec from `OpenProjectMenu`) and verify it, closing the largest untested surface.
2. ⬜ Integration smoke test for the interactive menus (Backup / Run / Provider / Pull / Overlord)
   that today are exercised only by hand.
3. ⬜ End-to-end `mindattic commit` exit-code assertions over a temp git repo.

### Audit log
No stories have been changed from an original spec yet; this is the first Codex pass. Future changed
stories preserve their original ask verbatim here, marked "(original spec — audit log)".
