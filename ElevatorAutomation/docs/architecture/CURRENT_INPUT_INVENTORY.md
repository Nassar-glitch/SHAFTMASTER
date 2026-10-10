# Current input inventory — Phase 1

Audit scope: ELEVATORPROJECT, UPDATEALL, ELEVATORMACHINEROOM, ELEVATORMRSECTION/ELEVATORREEVINGDATA, ELEVATORSECTIONAA/ELEVATORSECTIONAADATA, their readers, updaters and Core solvers. The abandoned VerticalExperiment generator is not an accepted view and is not a source of project defaults. In particular its speed=1 and CW clearance=300 defaults are NOT migrated.

Notation: P=Plan, M=Machine Room Plan, A=Section A-A, B=Section B-B. `Project` and `SectionAAProject` are Xrecords under NOD `ELEVATOR_AUTOMATION`. New grouped fields are inside versioned `ElevatorProject`. Null means NOT PROVIDED, not zero. All lengths are millimetres, angles degrees, capacity kg, speed m/s. E=engineering/layout-driving, D=drawing presentation, M=metadata only in accepted V1. Derived values are explicitly separated below.

## Existing project inputs and future UI fields

| Current name → unified field | Type/unit | Existing default; validity | Required / used by | Storage; E/D/M | Current prompt → future UI |
|---|---|---|---|---|---|
| SchemaVersion | int | 1 only | Required/all | Project; format | No; format-controlled |
| ElevatorType → Elevator.LegacyFamily + SystemId | enum/string | ExistingPlan; only validated current traction supported | Required/all | Project; E | ELEVATORPROJECT elevator type → system selector; unsupported choices disabled |
| TemplateId → Elevator.TemplateId | string | existing-plan-v1; must match manifest/catalog | Required/all | Project; E | No; selected by system/template catalog |
| ProjectName → Info.Name | string | null | Optional/annotations | Project; M/D | No current prompt → project name |
| SW → Shaft.WidthMm | double/mm | Measured from plan; finite >0 and rule-fit checks (smallest door needs SW≥1150 with current profile) | Required/all | Project; E | ELEVATORPROJECT SW → shaft width |
| SD → Shaft.DepthMm | double/mm | Measured; finite >0 plus car/CW clearance checks | Required/all | Project; E | ELEVATORPROJECT SD → shaft depth |
| Capacity → Elevator.CapacityKg | nullable double/kg | NOT PROVIDED; >0 | Required B/A, otherwise optional | Project; E preliminary beam | ELEVATORREEVINGDATA capacity, also requested by MRSECTION if missing → capacity |
| Speed → Elevator.SpeedMetresPerSecond | nullable double/m/s | NOT PROVIDED; >0 if supplied | Optional; no accepted solver uses it | Project; M | No → speed (verification unavailable) |
| Stops | nullable int/count | NOT PROVIDED; legacy placeholder | Optional metadata | Project; M | No; future derived served-floor count |
| Travel | nullable double/mm | NOT PROVIDED; legacy placeholder | Optional metadata | Project; M | No; future derived highest minus lowest served FFL |
| Pit → Shaft.PitDepthMm | nullable double/mm | Project may be null; A-A initializes 1000 | Required A, >0 finite | Project + SectionAAProject.Pit; E | A-A initial and PitOH → pit depth |
| Overhead → Shaft.OverheadMm | nullable double/mm | Project may be null; A-A initializes 4200 | Required A, >0 and top-floor/opening fit | Project + SectionAAProject.OH; E | A-A initial and PitOH → overhead |
| FloorHeights | double[]/mm | NOT PROVIDED legacy placeholder | Optional metadata until actual floor stack available | Project; M | No; replaced by floor sequence |
| PlanTitle → Drawing.PlanTitle | string | null retains native source title | Optional/P | Project; D | No (ELEVATORANNOTATION can bind tokens) → plan title |
| MachineRoomLeftExtension → MachineRoom.LeftExtensionMm | nullable double/mm | null uses measured template; supplied must match ±0.02 | Optional/M/B/A | Project; E | No → reference-driven/read-only in V1 |
| MachineRoomRightExtension → MachineRoom.RightExtensionMm | nullable double/mm | Same as left | Optional/M/B/A | Project; E | No → reference-driven/read-only |
| MachineRoomFrontExtension → MachineRoom.FrontExtensionMm | nullable double/mm | Same as left | Optional/M/B/A | Project; E | No → reference-driven/read-only |
| MachineRoomRearExtension → MachineRoom.RearExtensionMm | nullable double/mm | Same as left | Optional/M/B/A | Project; E | No → reference-driven/read-only |
| SuspensionRopeDiameterMm → Suspension.RopeDiameterMm | nullable double/mm | Unknown retained; allowed 6.5,8,10,11,12 | Optional numerical layout, required full component checks/M/B/A | Project; E | ELEVATORREEVINGDATA → rope diameter; no substitution |
| NumberOfSuspensionRopes → Suspension.RopeCount | nullable int/count | Prompt supplies 6 when absent; 4..7 | Optional Core, installation asks if absent/B/A | Project; E/metadata | ELEVATORREEVINGDATA → rope count; unified model does not silently default |
| TractionSheavePitchDiameterMm → Machine.TractionSheavePitchDiameterMm | nullable double/mm | Unknown or native 520; B rejects non-520 ±0.02 | Optional override/M/B/A | Project; E | No → selected-machine property (520 native restriction) |
| MinimumDeflectorSlabClearanceMm → MachineRoom.MinimumDeflectorSlabClearanceMm | nullable double/mm | NOT PROVIDED; finite ≥0 if supplied | Optional/B/A | Project; E | ELEVATORREEVINGDATA → specified minimum; unknown stays NOT YET EVALUABLE |
| DoorType → Doors.Type | string | NOT PROVIDED; not a geometry selector in V1 | Optional/P annotation metadata | Project; M | No → future door type |
| EntranceConfiguration → Doors.EntranceConfiguration | string | NOT PROVIDED; no variant geometry enabled | Optional/metadata | Project; M | No → future entrance configuration |
| CounterweightPosition → Counterweight.Position | string | NOT PROVIDED; actual arrangement from template | Optional/metadata | Project; M | No → future CW position |
| MachineType → Machine.Type | string | NOT PROVIDED; native machine retained | Optional/metadata | Project; M | No → future machine selection |
| RailType → Rails.RequestedRailType | string | NOT PROVIDED; projected native rail profiles retained | Optional/metadata | Project; M | No → future rail selection |
| SuspensionType → Suspension.Type | string | NOT PROVIDED | Optional/metadata | Project; M | No → future suspension selection |
| Extensions → Extensions | dictionary/object | Empty; unknown fields retained | Optional/all | Project; M/extensibility | No → extension-specific UI later |

Legacy Stops/Travel/FloorHeights do not define floor IDs or names. Migration preserves them as `legacy.*` metadata when no A-A floor stack exists; it does not fabricate a building. Once real floors exist, summaries are derived projections. If Project Pit/OH conflict with SectionAAProject, the A-A values are authoritative for migration, matching accepted drawing behavior.

## A-A inputs and interactive workflow

| Existing input → unified field | Type/unit | Default/range | Required / use | Storage; classification | Prompt → future UI |
|---|---|---|---|---|---|
| Floors[].FloorId → Building.Floors[].FloorId | stable string | Existing GUID retained; unique/nonempty | Required/A | SectionAAProject; E identity | Never typed → internal identity |
| Floors[].Name → Building.Floors[].Name | string | From BG/Numeric convention; unique/nonempty | Required/A | SectionAAProject; D | First name/Rename → floor name |
| Floors[].FflZ → derived FloorElevation.ElevationMm | double/mm | Lowest normal FFL 0; strictly increasing | Derived/A | Previously stored in SectionAAProject | Common/Individual intervals → HeightToNextMm input; elevation read-only |
| Floors[].Served → IsServed | bool | true for normal floors | Required/A | SectionAAProject; E/D | ARE THERE ANY NO STOP FLOORS? default No, then names → per-floor served toggle |
| Floors[].IsRoof | bool | Exactly one last unserved R | Structural role/A | SectionAAProject; derived role | No independent R elevation; result R=highest served+OH+MR slab |
| Number of normal floors | int | Prompt 2..99, calculator 2..100 | Required initial/A | Derived list count | Number prompt → floor list editor/count |
| Naming convention | choice | BG or Numeric | Initial convenience/A | Names persisted; not repeated authority | Convention → naming helper |
| First level | string | G for BG, 0 for Numeric; supports B and negative numbers | Initial convenience/A | Names persisted | First level → naming helper |
| Common/Individual height mode | choice | Common | Initial convenience/A | Actual intervals persisted | Mode → floor grid helper |
| Common/individual interval | double/mm | No invented default; finite positive; preceding served floor needs ≥2410 | Required/A | FFL deltas; E | Height prompts → HeightToNextMm; last null |
| Insert before / new name / added interval | identity/name/mm | Intermediate insertion; retains existing IDs | Optional edit/A | Floor list; E/D | Insert → floor editor |
| Remove normal floor | identity | Intermediate only, retains at least two floors | Optional edit/A | Floor list; E | Remove → floor editor |
| DisplayFloorId → Car.DisplayFloorId | string | G if available, else chosen served floor | Required/A | SectionAAProject; D/kinematic pose | Displayed car landing → FloorId selector |
| BracketMaximum → Rails.BracketMaximumSpacingMm | double/mm | Initial 1500; options 3000,2000,1500,1000/custom >0 | Required/A station solver | SectionAAProject; E provisional | Brackets → maximum spacing |
| BracketSource → Rails.BracketSource | string | PROJECT INPUT PENDING FUTURE CALCULATION SHEET | Required provenance/A | SectionAAProject; PROVISIONAL | No → provenance/read-only |
| ReferenceCarZ → Counterweight.ReferenceCarElevationMm | double/mm | 0 source pose | Required/A | SectionAAProject; E PROVISIONAL | No → calibration record |
| ReferenceCWBottomZ → Counterweight.ReferenceBottomElevationMm | double/mm | 12347.128322572622 audited source pose | Required/A | SectionAAProject; E PROVISIONAL | No → calibration record; never highest floor assumption |
| CWCalibrationStatus → Counterweight.CalibrationStatus | string | PROVISIONAL_SOURCE_REFERENCE_POSE | Required/A status | SectionAAProject | No → status display |
| Suspension.RopingRatio | nullable double/ratio | A-A migration explicitly records accepted 1:1; otherwise NOT PROVIDED | Current kinematics/A | New unified input | No → read-only 1:1 until other topology implemented |
| LandingDoorConfiguration | per-floor strings | NOT PROVIDED | Optional future metadata | New unified floor field | No old prompt → future floor-door editor; native V1 module unchanged |
| Insertion point / orientation | point/vector in drawing units | User-picked datum; relative to UCS/frame | Placement/M,A,B | Semantic native geometry | Three placement prompts → automatic extents-based layout |

## Reviewed rules/configuration, not independent project dimensions

All following values are engineering-driving, noninteractive and consumed by the shared solvers. They remain in the reviewed profile/rule set; Phase 1 does not make them editable project overrides.

| Config name | Type/unit | Accepted value/options | Views / origin / status |
|---|---|---|---|
| EquipmentProfile.Version | string | user-rules-v1 | All/config equipment-profile.json; provenance |
| CarRailSideAllowanceMm | double/mm | 150 | P/M/A/B; approved project rule |
| CarWidthDeductionMm | double/mm | 100 | P; approved |
| CarFrontAllowanceMm | double/mm | 125 | P/A; approved |
| CarRearAllowanceMm | double/mm | 150 | P/A; approved |
| ShaftRearAllowanceMm | double/mm | 125 | P/A; approved |
| TzAdditionMm | double/mm | 150 | All; approved |
| CounterweightRailRearAllowanceMm | double/mm | 125 | All; approved |
| CounterweightRailSideAllowanceMm | double/mm | 205 | P/M; fit rule |
| CounterweightRailToFrameEndMm | double/mm | 20 | P; fit rule |
| MinimumCabinDoorReturnMm | double/mm | 75 | P; door selection fit |
| DoorJambWidthMm | double/mm | 135 | P; native/profile |
| PreferredCounterweightDbgMm | double[]/mm | 1040,940,900,840,800,740,700,640,600 | P/M; largest fitting value |
| PreferredDoorWidthsMm | double[]/mm | 600,700,800,900,1000 | P; largest fitting value |
| Deflector market diameters | double[]/mm | Only 250,400 | M/B/A; TZ<520 none; 520..800→250; >800..1100→400; >1100 review |
| Governor offset | double/mm, derived | TZ<700→100; 700..<850→150; 850..1100→200; >1100 review | M/A; governor Ø250 independent |
| TargetTractionWrapAngleDeg | double/deg | 152 | B/A; project criterion, not complete traction verification |
| Concrete support range | double/mm | 350..1000 | B/A; geometric range, not structural adequacy |
| Hanger adjustability | bool | confirmed true | B/A; adjustable; mechanical review required |
| PreferredHeightIncrementMm | nullable double/mm | null; supplied rounding rejected | B solver; no rounding approved |
| StackedSteelLevels | int | 1 | B solver; native arrangement |
| Preliminary beam selection | catalog/string + nominal depth mm | Capacity<480: span<2200→UPN140 else160; ≤800→160; ≤1500→200; above→240 | B/A; PRELIMINARY/CALCULATION REQUIRED, not structural design |
| Car internal / overall height | double/mm | 2200 / 2400 | A; normalized native template, result constants |
| Car envelope bottom offset | double/mm | -100 relative car FFL; top2300 | A; native master geometry |
| CW overall height | double/mm | 3000 | A; normalized V1 template |
| Landing door clear / frame height | double/mm | 2000 / 2160 | A; native module; no automatic dimensions |
| Landing slab graphical thickness | double/mm | 250 | A; native module |
| MR clear height | double/mm | 2200 | A; V1 approved architectural default |
| Car/CW rail profile | string | T70/65/9 and T50/50/5 | A from accepted Plan; end adequacy NOT_YET_EVALUABLE |
| Bracket hardware / fixing / rail joints | component/status | NOT PROVIDED / NOT_YET_EVALUABLE | A; stations only, no physical hardware invented |

## Native template dependencies

These are mandatory measured/reference inputs, not interactive project fields. They are stored in semantic bindings/manifests, XData and native geometry. They are supplied to Core as `DesignContext` or existing geometry snapshots. Their valid range is the audited template topology, not a newly selected engineering range.

| Names / dependency | Type/unit | Origin/default | Consumers / classification |
|---|---|---|---|
| Existing CwDepth | double/mm | Measured Plan; retained by DesignRules, must fit | P/all; E |
| ShaftFrame.Origin/XAxis/YAxis/ZAxis | point/vectors | Shaft inner faces + entrance datum | All; D/reference frame |
| Plan binding roles, contact lines, cabin/door walls and support extents | semantic IDs + local coordinates | template-bindings.json/native Plan | All; E measured reference, no runtime handles |
| MR Left/Right/Front/Rear extensions | doubles/mm | Measured source view | M/B/A; E fixed reference |
| MR P1/P3 rear support offsets, P2/P4 front support offsets | doubles/mm | Native dependency geometry, averaged by side | B/A; E; no new reaction calculation |
| MR PitchProjection / retained visibility | double/mm + bool | Current valid native 250/400 detail | B/A; retained during out-of-range review |
| SectionReference.SourceCarRow, SourceCwRow, SourceSD | doubles/mm | B-B source manifest | B/A; E reference |
| SourceTractionX, SourceTractionZ, SourceDeflectorX, SourceDeflectorZ | doubles/mm | Reviewed B-B circles/datums | B/A; E reference |
| SourceSteelBottomZ, SourceSteelTopZ, SourceStandBottomZ | doubles/mm | Reviewed steel/stand geometry | B/A; E fixed stack offsets |
| SourceConcreteHeight, SourceFrontSupportX, SourceRearSupportX | doubles/mm | Reviewed B-B reference | B/A; E reference |
| SourceSlabThickness | double/mm | Coordinated B-B native slab, baseline200 | A/B; E; R is derived from this |
| CarOpeningWidth, CwOpeningWidth | doubles/mm | Existing B-B openings | B/A; opening engineering review required |
| HangerTopBelowSteelTop, HangerBottomAboveDeflectorCenter | doubles/mm | B-B hanger source offsets | B/A; E reference; no strength claim |
| A-A canonical Floor2 source / 2221-part native library | tagged geometry | section-aa-seed.json + cleaned master | A; source handles confined to hash-gated bootstrap |
| A-A pit ownership, buffers, governor, hook, P6/P7 notes | tagged native members | Audited source + source pose | A; E/D; reactions and hook capacity reference-only |
| A-A car/CW X anchor zones and span/end groups | local measured coordinates | Cleaned native library | A; E; no global scaling |
| AnnotationTemplate, native text/dimension styles, hatch pattern/scale | strings/style IDs | Binding metadata/source DWGs | All; D; preserved |
| Source manifests/dependency recipes and hashes | versioned objects | seed files + DWG Xrecords | All; provenance/topology validation |

## Derived design/result inventory

These are never separate authoritative editable inputs in ElevatorProject. Current legacy calculated records remain compatibility caches/results.

| Result names | Type/unit | Calculation / consumers |
|---|---|---|
| SW, SD | double/mm | Echo input/all |
| CarDbg, CarWidth, CarDepth, CarFront, CarRear | double/mm | Existing profile sizing rules/P,A |
| TZ, CarRow, CwRow | double/mm | Existing shared design/all |
| CwDbg, CwWidth, CwDepth, CwFront, CwRear | double/mm | Largest-fit DBG; retained template depth/P,M,A |
| DoorWidth, JambWidth, Center, all X/Y named anchors | double/mm | Existing door rule and shaft frame/P |
| RoomWidth, RoomDepth, Extensions | double/mm | Shaft plus measured room margins/M |
| Deflector.IsRequired/PitchDiameterMm/SelectionStatus | bool/double/status | TZ rule/M,B,A |
| Governor.OffsetFromCarGuideAxisMm/SelectionStatus | nullable double/status | Confirmed TZ thresholds/M,A |
| Beam.SectionDesignation/NominalDepthMm/SpanMm/CapacityKg/StructuralStatus | string/doubles/status | Preliminary table/B,A; ReactionN, moment, shear, stress, deflection, utilization remain null |
| FrontSupportX, RearSupportX, SteelBottomZ, SteelTopZ, ConcreteHeight | doubles/mm | Shared B-B geometry/B,A |
| TractionX/Z, DeflectorX/Z, DeflectorPitchMm, CarDropX, CwDropX | doubles/mm | Shared reeving solution/B,A |
| TractionSheavePitchDiameter, DeflectorSheavePitchDiameter | double/mm | Approved native machine + selected 250/400/B,A |
| Horizontal/VerticalCenterOffset, CenterDistance, sheave centers and tangent points | doubles/points mm | ReevingSolver/B,A |
| RopeEntryDirection/RopeExitDirection, RopeEntryAngle/RopeExitAngle | vectors/deg | ReevingSolver/B,A |
| CalculatedWrapAngle, TargetWrapAngle | double/deg | ReevingSolver; 152 target/B,A |
| RequiredVerticalCenterSeparation, RequiredDeflectorCenterZ, RequiredTractionSheaveCenterZ, RequiredHangerLength | nullable double/mm | Existing adjustable-hanger solver/B,A |
| DeflectorToSlabClearance, ConcreteSupportHeight, RequiredConcreteBeamHeight, RequiredChassisElevationAdjustment | nullable double/mm | Existing solver/B,A |
| Floor elevations, PitZ, Underside, R, Ceiling, CarZ, CWBottom, Travel | doubles/mm | Floor sequence + approved A-A formulas/A; CW delta=-car delta |
| Bracket elevations/consecutive spacings | lists/mm | Existing BracketLayoutSolver/A |
| LayoutStatus, WrapAngleStatus, CodeDiameterRatioStatus | independent status | No code warning silently changes diameter or blocks otherwise valid layout |
| RailEndAdequacy, slab clearance, hanger/beam/concrete/opening/rope-clearance statuses | status | Remain explicit NOT_YET_EVALUABLE / REVIEW_REQUIRED where inputs absent |

## Standards and missing engineering data

Existing rule registry retains the supplied EN 81-20:2014 interpretation and clauses; Phase 1 does not re-interpret or certify standards. Minimum rope8 and D/d40 remain independent numeric checks. Ø250 code conflict remains visible. Full EN 81-50 traction calculation, masses/loads, groove/friction data, rope construction/breaking loads, safety factors, manufacturer pitch/outer diameters, guards, maintenance/access zones, ventilation, ferrules, governor certification/rope diameter, rail end/fixing/joint details, bracket hardware, hanger strength/adjustment limits, concrete reinforcement/bearing and final CW buffer calibration are NOT PROVIDED or NOT YET EVALUABLE. Company/manufacturer constraint documents are nullable; no values are invented.

New UI-only foundation fields: Info.ProjectId (stable generated identity), Elevator.DriveType (NOT PROVIDED), Standards rule-set/provenance fields, Drawing.ViewMarginMm (1000 presentation default), Drawing.Arrangement (TwoByTwo). None changes accepted engineering geometry.
