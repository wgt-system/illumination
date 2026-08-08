# Illumination – Scheduling Simulation

These examples validate the initial V1 defaults. They are not separate domain rules.

| Scenario | Stability after each review (days) | Final difficulty |
|---|---:|---:|
| new → Gut → Gut → Leicht | 2.0 → 3.99 → 12.9 | 4.15 |
| new → Schwer → Gut → Gut | 0.28 → 2.0 → 3.89 | 5.2 |
| new → Nochmal → Gut → Gut | 0.12 → 2.0 → 3.79 | 5.8 |
| new → Unsicher → Gut → Gut | 1.0 → 2.0 → 3.96 | 4.75 |
| 60d stable → Nochmal → Gut | 3.0 → 5.88 | 5.0 |
| 60d stable → Schwer → Gut | 7.0 → 14.07 | 4.4 |

## Interpretation

- New material that receives repeated positive Reviews grows from days into longer intervals.
- `Nochmal` and `Schwer` additionally trigger same-session relearning.
- A lapse after long-term stability retains history but is capped so one successful retry cannot immediately restore a multi-week interval.
- `Unsicher` keeps material comparatively active.
- `Leicht` accelerates interval growth without producing an automatic `Mastered` state.

The numeric constants remain tunable defaults; the semantic ordering is fixed.
