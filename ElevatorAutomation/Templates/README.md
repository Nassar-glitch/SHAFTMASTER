# Master template readiness

| Type | Master | Status |
|---|---|---|
| ExistingPlan | ExistingPlan_Master.dwg | Prepared from the preserved original DWG; all 521 entities tagged, geometry unchanged |
| Hydraulic | Hydraulic_Master.dwg | Reserved filename; actual family drawing/profile required |
| MRL Gearless | MRL_Gearless_Master.dwg | Reserved filename; actual family drawing/profile required |
| Gearbox | Gearbox_Master.dwg | Reserved filename; actual family drawing/profile required |
| HomeLift | HomeLift_Master.dwg | Reserved filename; actual family drawing/profile required |

The four reserved files are deliberately not fabricated by renaming the existing drawing. Set up each actual family master once, with its own template ID and profile in `config/families.json`. Small variants should use parameters/rules within that family. The prepared existing master has embedded bindings and no project inputs, so project entry runs once on its first use.
