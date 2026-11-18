# TaskFlow technical debt register

Debt is a decision to take a shortcut and pay for it later. This register makes that
decision visible: what the shortcut is, what it costs to fix (principal), what it costs
every week we leave it (interest), and what we decided.

Cost is an estimate. Interest is evidence: churn comes from `git log`, the rest from the
failures and test runs shown in episode 4.4.

| # | Item | Cost to fix | Interest we are paying | Decision |
|---|------|-------------|------------------------|----------|
| 1 | `FakeTaskRepository.Update` threw `NotImplementedException`; the fake also differed from the real stores | 20 minutes | Any test that touched Update crashed; the contract suite found 5 failures in the fake | **Paid** in this PR |
| 2 | `Program.cs` mixes configuration, the pipeline, endpoints and teaching probes (338 lines) | 1 day | Changed in 9 of the first 35 commits: the most edited file | Next: move endpoints into groups |
| 3 | The comment endpoint catches `Exception` and returns a generic 500 | 30 minutes | Duplicates the global handler and hid the cause from the caller | Ticket |
| 4 | `TaskService` has three jobs: tasks, stats, comments | half a day | 4 of the first 35 commits touched it | Wait: split when a fourth use case arrives |
| 5 | `ITaskRepository` has 7 members, so every fake implements all 7 | half a day | Every new fake repeats the work | Wait: split reader and writer with item 4 |
| 6 | The clock is read directly: 7 `UtcNow` calls in 4 files | 2 hours | Date rules cannot be tested at a fixed instant | Ticket: inject `TimeProvider` |
| 7 | The teaching probes `/diag`, `/diag/boom` and `/diag/boom-middleware` were mapped in every environment, Production included | 10 minutes | In a Production container any caller got 200 on `/diag` and 500 on both boom routes, and each boom call logged an exception | **Paid** in this PR: mapped in Development only |
| 8 | `Microsoft.OpenApi` 2.0.0, which comes in through `Microsoft.AspNetCore.OpenApi`, has a known high severity advisory (GHSA-v5pm-xwqc-g5wc) | Unknown until tried | Every restore prints warning NU1903, and the vulnerable package is in the image | Ticket: try a newer `Microsoft.AspNetCore.OpenApi` |
| 9 | `/binding/{id}` is also mapped in every environment and echoes the `X-Api-Key` header back in its answer | 10 minutes | A teaching probe in Production that reflects a secret header | Ticket: same Development-only treatment as item 7 |

Rule of thumb: pay first what is cheap and expensive to leave (item 1). Leave what is
expensive and not hurting yet (items 4 and 5), and write down why.
