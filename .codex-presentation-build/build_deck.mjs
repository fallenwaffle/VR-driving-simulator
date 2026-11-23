import fs from 'node:fs/promises';
import path from 'node:path';
import { Presentation, PresentationFile } from 'file:///C:/Program%20Files/WindowsApps/OpenAI.CodexPrimaryRuntime.v26-1110-973-0_26.1110.973.0_x64__3k8sg7r9htsxt/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
import { pathToFileURL } from 'node:url';
import { finalizePresentation } from 'file:///C:/Users/naksh/.codex/plugins/cache/openai-primary-runtime/presentations/26.1110.11613/skills/presentations/container_tools/artifact_tool_utils.mjs';

const ROOT = process.cwd();
const BUILD = path.join(ROOT, '.codex-presentation-build');
const OUTPUT = path.join(ROOT, '.codex-presentation-output');
const FINAL_PPTX = path.join(OUTPUT, 'VR_Driver_Work_Sampling_Ergonomics_Final_v3.pptx');
const FONT = 'Arial';
const C = {
  plum: '#581845',
  charcoal: '#1C1C1E',
  silver: '#E0E0E0',
  white: '#FFFFFF',
  navy: '#000080',
  cream: '#F0EAD6',
  rose: '#E8C3BA',
  red: '#FF3B30',
  muted: '#635F66',
  lightMuted: '#A8A2AA',
  pale: '#F8F5EF',
  green: '#54735E',
  blueGray: '#778392',
};

const W = 1280, H = 720;
const P = Presentation.create({ slideSize: { width: W, height: H } });

function shape(slide, geometry, x, y, w, h, fill = 'none', lineFill = 'none', lineWidth = 0, radius = null, name = undefined) {
  const cfg = {
    geometry,
    name,
    position: { left: x, top: y, width: w, height: h },
    fill,
    line: { style: 'solid', fill: lineFill, width: lineWidth },
  };
  if (radius != null) cfg.borderRadius = radius;
  return slide.shapes.add(cfg);
}

function text(slide, value, x, y, w, h, opts = {}) {
  const s = slide.shapes.add({
    geometry: 'textbox',
    name: opts.name,
    position: { left: x, top: y, width: w, height: h },
    fill: 'none',
    line: { style: 'solid', fill: 'none', width: 0 },
  });
  s.text = value;
  s.text.style = {
    typeface: opts.font || FONT,
    fontSize: opts.size ?? 20,
    bold: opts.bold ?? false,
    italic: opts.italic ?? false,
    color: opts.color || C.charcoal,
    alignment: opts.align || 'left',
    verticalAlignment: opts.valign || 'top',
    lineSpacing: opts.lineSpacing ?? 1.08,
    wrap: 'square',
    autoFit: opts.autoFit || 'shrinkText',
    insets: opts.insets ?? 0,
  };
  return s;
}

function rule(slide, x1, y1, x2, y2, color = C.silver, width = 1) {
  return slide.shapes.add({
    geometry: 'line',
    position: { left: Math.min(x1, x2), top: Math.min(y1, y2), width: Math.abs(x2 - x1), height: Math.abs(y2 - y1) },
    flipV: (x2 - x1) * (y2 - y1) < 0,
    fill: 'none',
    line: { style: 'solid', fill: color, width },
  });
}

function label(slide, str, x, y, w = 220, color = C.navy) {
  return text(slide, str.toUpperCase(), x, y, w, 22, { size: 14, bold: true, color, lineSpacing: 1 });
}

function footer(slide, n, dark = false) {
  const col = dark ? '#CFC8D0' : C.muted;
  rule(slide, 64, 680, 1216, 680, dark ? '#71566D' : '#D9D2C8', 1);
  text(slide, 'WORK SYSTEMS DESIGN & ERGONOMICS LAB', 64, 689, 600, 18, { size: 11, color: col, bold: true, lineSpacing: 1 });
  text(slide, String(n).padStart(2, '0'), 1176, 688, 40, 20, { size: 13, color: col, align: 'right', lineSpacing: 1 });
}

function header(slide, n, title, section, subtitle = '') {
  slide.background.fill = C.cream;
  label(slide, section, 64, 36, 440, C.plum);
  text(slide, title, 64, 66, 1120, 54, { size: 39, color: C.charcoal, bold: true, lineSpacing: 1 });
  if (subtitle) text(slide, subtitle, 66, 124, 1110, 35, { size: 18, color: C.muted });
  footer(slide, n, false);
}

function notes(slide, content) {
  slide.speakerNotes.text = content;
  slide.speakerNotes.setVisible(true);
}

async function imageBytes(file) {
  const b = await fs.readFile(path.join(BUILD, 'assets', file));
  return b.buffer.slice(b.byteOffset, b.byteOffset + b.byteLength);
}

function addTable(slide, values, x, y, w, h, opts = {}) {
  const rows = values.length, cols = values[0].length;
  const t = slide.tables.add({ rows, columns: cols, left: x, top: y, width: w, height: h, values });
  t.borders.assign({ style: 'solid', fill: opts.border || '#D8D0C6', width: 1 });
  const all = t.cells.block({ row: 0, column: 0, rowCount: rows, columnCount: cols });
  all.textStyle.fontSize = opts.size || 16;
  all.textStyle.typeface = FONT;
  all.textStyle.color = C.charcoal;
  all.margins = { top: 7, bottom: 7, left: 10, right: 10 };
  all.anchor = 'middle';
  for (let r = 0; r < rows; r++) {
    for (let c = 0; c < cols; c++) {
      const cell = t.getCell(r, c);
      cell.fill = r === 0 ? C.plum : (r % 2 ? C.white : C.pale);
    }
  }
  const top = t.cells.block({ row: 0, column: 0, rowCount: 1, columnCount: cols });
  top.textStyle.bold = true;
  top.textStyle.color = C.white;
  top.textStyle.typeface = FONT;
  if (opts.firstColumnBold) {
    const first = t.cells.block({ row: 1, column: 0, rowCount: rows - 1, columnCount: 1 });
    first.textStyle.bold = true;
  }
  return t;
}

function addBullets(slide, items, x, y, w, size = 22, gap = 54, color = C.charcoal, bulletColor = C.navy) {
  items.forEach((item, i) => {
    shape(slide, 'ellipse', x, y + i * gap + 7, 9, 9, bulletColor);
    text(slide, item, x + 24, y + i * gap, w - 24, gap - 6, { size, color, lineSpacing: 1.1 });
  });
}

// 1 — cover
{
  const s = P.slides.add(); s.background.fill = C.plum;
  shape(s, 'rect', 0, 0, 14, H, C.rose);
  text(s, 'VR DRIVER\nWORK SAMPLING\nSIMULATOR', 72, 108, 694, 206, { size: 54, color: C.white, bold: true, lineSpacing: 0.98 });
  text(s, 'An ergonomics and work-systems study of driving under changing visibility', 76, 341, 630, 82, { size: 25, color: '#E8DFE6', lineSpacing: 1.12 });
  shape(s, 'rect', 76, 467, 454, 2, C.rose);
  text(s, 'Work Systems Design and Ergonomics Lab', 76, 490, 598, 34, { size: 21, color: C.white, bold: true });
  text(s, 'College presentation  ·  Team / presenter: __________________', 76, 534, 600, 28, { size: 16, color: '#D9CCD8' });
  text(s, 'UNITY 6  /  VEHICLE PHYSICS PRO  /  OPENXR  /  META QUEST 3', 76, 623, 640, 20, { size: 13, color: C.rose, bold: true });
  // Subtle editable road motif, built from lane lines rather than a stock graphic.
  shape(s, 'rect', 822, 0, 458, H, '#261421');
  rule(s, 880, 0, 1110, 720, '#C4B5C3', 2);
  rule(s, 962, 0, 1190, 720, '#C4B5C3', 2);
  for (let i = 0; i < 9; i++) shape(s, 'rect', 1030 + i * 9, 54 + i * 73, 5, 34, C.rose);
  text(s, 'CLEAR  /  FOG', 870, 36, 260, 24, { size: 14, color: C.rose, bold: true, align: 'center' });
  footer(s, 1, true);
  notes(s, 'Open by describing the project as both a driving simulator and an instrumented work-system prototype. State that the study compares Clear and Fog visibility and captures randomized work samples. Add presenter and college details before presenting.');
}

// 2 — question and framing
{
  const s = P.slides.add(); header(s, 2, 'Driving is a work-system outcome', '01  /  RESEARCH FRAME');
  text(s, 'A driver’s observed work changes as the task, interface, vehicle response, and road environment change together.', 66, 150, 1080, 64, { size: 28, color: C.plum, bold: true, lineSpacing: 1.08 });
  const blocks = [
    ['PERSON', 'Perceive cues\nDecide\nAct'],
    ['TASK', 'Maintain path\nControl speed\nRespond to hazards'],
    ['TECHNOLOGY', 'VPP vehicle\nControls + HUD\nVR tracking'],
    ['ENVIRONMENT', 'Urban road\nVisibility\nMarked zones'],
  ];
  const xs = [66, 352, 638, 924];
  blocks.forEach((b, i) => {
    shape(s, 'rect', xs[i], 260, 238, 190, i === 2 ? '#E9E4EC' : C.white, '#D8D0C6', 1, 12);
    label(s, b[0], xs[i] + 18, 280, 195, C.navy);
    text(s, b[1], xs[i] + 18, 316, 200, 105, { size: 24, color: C.charcoal, bold: true, lineSpacing: 1.18 });
    if (i < blocks.length - 1) text(s, '→', xs[i] + 244, 335, 40, 40, { size: 30, color: C.rose, bold: true, align: 'center' });
  });
  shape(s, 'rect', 66, 495, 1080, 96, C.plum, 'none', 0, 12);
  label(s, 'STUDY QUESTION', 88, 515, 205, C.rose);
  text(s, 'How does Clear versus Heavy Fog visibility relate to observed driver work states and speed at marked zones?', 88, 546, 1020, 38, { size: 23, color: C.white, bold: true });
  notes(s, 'Keep the question observational. The prototype records behavior in two visibility conditions, but it does not by itself prove that fog caused an outcome or predict real-world crash risk. Sources: EXPERIMENT_GUIDE.md; Assets/ExperimentManager.cs.');
}

// 3 — systems view
{
  const s = P.slides.add(); header(s, 3, 'The system under study', '01  /  RESEARCH FRAME', 'The simulator makes key parts of a driving work system repeatable and observable.');
  const cx = 640, cy = 382;
  shape(s, 'ellipse', cx - 108, cy - 66, 216, 132, C.plum);
  text(s, 'DRIVING\nTASK', cx - 76, cy - 34, 152, 68, { size: 24, color: C.white, bold: true, align: 'center', valign: 'middle' });
  const nodes = [
    { x: 82, y: 234, w: 318, h: 122, cap: 'OPERATOR', body: 'Driver perception\nand control actions', fill: C.white },
    { x: 880, y: 234, w: 318, h: 122, cap: 'ENVIRONMENT', body: 'Road geometry\nvisibility and zones', fill: '#E9E4EC' },
    { x: 82, y: 445, w: 318, h: 122, cap: 'TECHNOLOGY', body: 'VPP vehicle\nQuest / keyboard / HUD', fill: '#EEE4E3' },
    { x: 880, y: 445, w: 318, h: 122, cap: 'OBSERVATION', body: 'Random snapshots\nand zone outcomes', fill: C.white },
  ];
  nodes.forEach((n) => {
    shape(s, 'rect', n.x, n.y, n.w, n.h, n.fill, '#D8D0C6', 1, 12);
    label(s, n.cap, n.x + 18, n.y + 16, n.w - 36, C.navy);
    text(s, n.body, n.x + 18, n.y + 46, n.w - 36, 62, { size: 21, color: C.charcoal, bold: true });
  });
  rule(s, 400, 286, 520, 344, C.navy, 2); rule(s, 760, 344, 880, 286, C.navy, 2);
  rule(s, 400, 495, 526, 425, C.plum, 2); rule(s, 754, 425, 880, 495, C.plum, 2);
  text(s, 'Condition: Clear or Fog', 505, 565, 270, 25, { size: 17, color: C.muted, italic: true, align: 'center' });
  notes(s, 'Explain that the unit of interest is the whole driving work system, not just vehicle speed. The observer records a rule-based work state; the system does not currently measure gaze or subjective workload. Source: Assets/WorkSamplingObserver.cs.');
}

// 4 — architecture
{
  const s = P.slides.add(); header(s, 4, 'From driver input to exported evidence', '02  /  SYSTEM');
  const y = 244, bh = 136, bw = 206, gap = 27, x0 = 66;
  const stages = [
    ['INPUT', 'Keyboard\nMeta Quest 3\nTouch Plus'],
    ['SIMULATION', 'Unity 6\nVPP vehicle\ncity scene'],
    ['EXPERIMENT', 'Clear / Fog\n120 s runs\nzone triggers'],
    ['OBSERVER', '2–4.5 s\nrandom samples\nstate classifier'],
    ['EXPORT', 'CSV samples\nzones + summary\ncomparison report'],
  ];
  stages.forEach((st, i) => {
    const x = x0 + i * (bw + gap);
    shape(s, 'rect', x, y, bw, bh, i === 2 ? '#E9E4EC' : C.white, '#D8D0C6', 1, 12);
    label(s, st[0], x + 16, y + 16, bw - 32, i === 2 ? C.plum : C.navy);
    text(s, st[1], x + 16, y + 51, bw - 32, 76, { size: 20, color: C.charcoal, bold: true, lineSpacing: 1.08 });
    if (i < stages.length - 1) text(s, '→', x + bw + 2, y + 48, 25, 36, { size: 25, color: C.rose, bold: true, align: 'center' });
  });
  shape(s, 'rect', 66, 438, 1148, 120, C.charcoal, 'none', 0, 12);
  label(s, 'IMPLEMENTED OUTPUTS', 88, 457, 280, C.rose);
  text(s, 'samples.csv   ·   zones.csv   ·   summary.csv   ·   comparison.md', 88, 495, 1065, 34, { size: 22, color: C.white, bold: true });
  text(s, 'In the Unity Editor, files go to the project Exports folder; builds use persistent storage.', 88, 531, 1060, 22, { size: 16, color: '#DAD4DB' });
  notes(s, 'Trace one throttle input through VPP vehicle motion, randomized observer snapshots, and CSV export. CSVExporter writes raw sample rows, zone outcomes, summary statistics, and a Markdown comparison. Sources: Assets/Quest3DrivingInput.cs; Assets/ExperimentManager.cs; Assets/WorkSamplingObserver.cs; Assets/CSVExporter.cs.');
}

// 5 — road environment screenshot
{
  const s = P.slides.add(); header(s, 5, 'Urban roads create the driving context', '02  /  SYSTEM', 'The built-in city scene provides streets, junctions, buildings, crosswalks and visible hazard markers.');
  const bytes = await imageBytes('city-run.png');
  s.images.add({ blob: bytes, contentType: 'image/png', alt: 'Unity Game view showing SUV on urban road with warning zone markers and Work Sampling HUD', fit: 'cover', position: { left: 66, top: 190, width: 1148, height: 430 }, geometry: 'roundRect', borderRadius: 12 });
  shape(s, 'rect', 66, 634, 1148, 26, C.plum, 'none', 0, 8);
  text(s, 'LIVE PROJECT SCREENSHOT  ·  CITY SCENE  ·  WARNING ZONES ARE CUSTOM OVERLAYS', 82, 638, 1112, 18, { size: 13, color: C.white, bold: true });
  notes(s, 'The screenshot is supplied by the project team and shows the Unity scene during a run. The city environment is part of the project’s imported VPP sample assets; this deck does not claim the network models real traffic flow. Source: team screenshot; Assets/Scenes/SUV.unity; VPP Sample Assets/Prefabs/The City.prefab.');
}

// 6 — vehicles and physics
{
  const s = P.slides.add(); header(s, 6, 'Two vehicles, one VPP simulation stack', '02  /  SYSTEM');
  shape(s, 'rect', 66, 178, 510, 210, C.white, '#D8D0C6', 1, 12);
  label(s, 'SUV  /  JPickup', 90, 200, 380, C.navy);
  text(s, 'Utility vehicle\n\nSelectable VPP drive modes\nManual and automatic behavior\nEngine, clutch and gearbox setup', 90, 236, 438, 130, { size: 22, color: C.charcoal, bold: true, lineSpacing: 1.1 });
  shape(s, 'rect', 704, 178, 510, 210, '#E9E4EC', '#D8D0C6', 1, 12);
  label(s, 'SPORT COUPE', 728, 200, 380, C.plum);
  text(s, 'Performance vehicle\n\nManual transmission setup\nOptional auto-shift configuration\nVehicle-specific gearing and dynamics', 728, 236, 438, 130, { size: 22, color: C.charcoal, bold: true, lineSpacing: 1.1 });
  text(s, 'VPP integration in this project', 66, 429, 510, 34, { size: 26, color: C.plum, bold: true });
  addBullets(s, [
    'VehicleBase and VPP input data connect control input to vehicle response',
    'Configured engine, clutch, gearbox, braking and differential behavior',
    'Driver-assist indicators include ABS, ESC, TCS and ASR where configured',
  ], 70, 476, 1095, 52, 31, C.charcoal, C.navy);
  text(s, 'Transmission behavior depends on each vehicle’s VPP setup; do not assume identical gearboxes.', 70, 615, 1100, 26, { size: 16, color: C.muted, italic: true });
  notes(s, 'The scenes instantiate a VPP JPickup as the SUV and a VPP Sport Coupe. The project uses VPP configuration rather than a custom physics model. The SUV demo UI describes M/P/R/N/D/L modes; the Sport Coupe scene describes a manual setup with optional auto-shift. Avoid claiming both vehicles use identical transmission configurations. Sources: Assets/Scenes/SUV.unity; Assets/Scenes/Sports Car.unity; VPP vehicle prefabs.');
}

// 7 — control design and keyboard/Quest split
{
  const s = P.slides.add(); header(s, 7, 'One task, two input systems', '03  /  INTERACTION', 'Quest 3 is the immersive path; keyboard remains the desktop fallback.');
  const quest = [
    ['Touch Plus input', 'Driving action'],
    ['Left stick X', 'Steer'], ['Right trigger', 'Throttle'], ['Left trigger', 'Brake'],
    ['Right grip', 'Handbrake'], ['Left grip', 'Clutch (manual)'],
    ['Right stick ↑ / ↓', 'Shift up / down'], ['Right stick click', 'Recenter view'],
    ['A / B / X / Y', 'Ignition / reverse / camera / reset'],
  ];
  const kb = [
    ['Keyboard input', 'Driving action'],
    ['WASD / arrows', 'Steer, throttle, brake'], ['Space', 'Handbrake'], ['Shift', 'Clutch'],
    ['Tab / Caps Lock', 'Manual gear up / down'], ['K / R / Enter', 'Ignition / reverse / reset'],
    ['C / F1–F4', 'Camera changes'],
  ];
  addTable(s, quest, 66, 188, 690, 382, { size: 17, firstColumnBold: true });
  addTable(s, kb, 786, 188, 428, 320, { size: 16, firstColumnBold: true });
  shape(s, 'rect', 786, 526, 428, 98, C.plum, 'none', 0, 12);
  label(s, 'EXPERIMENT MODIFIER', 805, 542, 250, C.rose);
  text(s, 'Hold left stick click: A Clear · B Fog · X end · Y zone · right stick down undo · right stick click export', 805, 572, 390, 42, { size: 16, color: C.white, bold: true, lineSpacing: 1.05 });
  notes(s, 'The Quest controls follow the current project mapping. The left stick click is a modifier for experiment actions, which separates them from ordinary driving. Some keyboard mappings come from VPP’s existing demo UI; verify exact keys in the active scene if the instructor asks. Sources: Assets/Quest3DrivingInput.cs; Assets/ExperimentManager.cs; scene help panels.');
}

// 8 — cockpit screenshot and HUD
{
  const s = P.slides.add(); header(s, 8, 'Driver view and glanceable feedback', '03  /  INTERACTION', 'The cockpit view couples head-tracked looking with vehicle-state information.');
  const bytes = await imageBytes('driver-view.png');
  s.images.add({ blob: bytes, contentType: 'image/png', alt: 'First-person cockpit screenshot with steering wheel, road, buildings and gauges', fit: 'cover', position: { left: 66, top: 188, width: 702, height: 438 }, geometry: 'roundRect', borderRadius: 12 });
  label(s, 'HUD / DASHBOARD', 812, 199, 320, C.navy);
  addBullets(s, [
    'Speedometer: current speed',
    'Tachometer: engine RPM',
    'Gear / drive mode and ignition state',
    'Vehicle-aid indicators and control status where configured',
    'Camera and recenter actions remain available in VR',
  ], 812, 236, 384, 20, 59, C.charcoal, C.plum);
  shape(s, 'rect', 812, 558, 384, 66, '#E9E4EC', 'none', 0, 10);
  text(s, 'ERGONOMIC INTENT', 830, 570, 170, 18, { size: 13, color: C.plum, bold: true });
  text(s, 'Present essential status close to the driving task; test legibility and glance demand with participants.', 830, 591, 348, 26, { size: 15, color: C.charcoal, bold: true });
  notes(s, 'This is the team-supplied in-car screenshot. It shows the road, cockpit and physical instrument cluster. The HUD gives vehicle state but the project does not currently record eye movements, glance durations or subjective workload. Source: team screenshot; scene dashboard assets.');
}

// 9 — protocol timeline
{
  const s = P.slides.add(); header(s, 9, 'Clear and Fog runs on a repeated route', '04  /  EXPERIMENT', 'A within-driver comparison is configured; the condition sequence is controlled by the operator.');
  const xs = [90, 346, 602, 858, 1114];
  const items = [
    ['01', 'Practice', 'Learn controls\nand route'],
    ['02', 'Run 1', 'Clear\n120 s default'],
    ['03', 'Reset', 'Return car to\nstart position'],
    ['04', 'Run 2', 'Fog\n120 s default'],
    ['05', 'Export', 'Save CSVs +\ncomparison'],
  ];
  rule(s, 112, 296, 1137, 296, '#CFC4CB', 3);
  items.forEach((it, i) => {
    shape(s, 'ellipse', xs[i] - 26, 270, 52, 52, i === 3 ? C.rose : C.plum);
    text(s, it[0], xs[i] - 22, 283, 44, 20, { size: 16, color: i === 3 ? C.plum : C.white, bold: true, align: 'center', valign: 'middle' });
    text(s, it[1], xs[i] - 78, 340, 156, 30, { size: 22, color: C.charcoal, bold: true, align: 'center' });
    text(s, it[2], xs[i] - 86, 380, 172, 64, { size: 17, color: C.muted, align: 'center', lineSpacing: 1.1 });
  });
  shape(s, 'rect', 66, 504, 1148, 82, C.white, '#D8D0C6', 1, 12);
  text(s, 'Random observation interval', 88, 520, 310, 24, { size: 18, color: C.charcoal, bold: true });
  text(s, '2.0–4.5 seconds', 88, 548, 285, 26, { size: 23, color: C.navy, bold: true });
  text(s, '≈ 37 snapshots in a 120-second run on average', 486, 539, 646, 34, { size: 21, color: C.plum, bold: true, align: 'right' });
  notes(s, 'The observer samples at a random 2–4.5 second interval. The 120-second duration is configurable and is the current default. The current front-panel controls provide Clear and Fog runs; counterbalancing order across participants is a recommended study protocol rather than an automated feature. Sources: Assets/ExperimentManager.cs; Assets/WorkSamplingObserver.cs; EXPERIMENT_GUIDE.md.');
}

// 10 — state rules
{
  const s = P.slides.add(); header(s, 10, 'Four mutually exclusive work states', '04  /  EXPERIMENT', 'Rules are checked in order; the first matching rule determines the sample label.');
  const states = [
    { n: '01', title: 'IDLE / DELAY', rule: 'Speed < 3 km/h', explain: 'Vehicle is stopped or nearly stopped', color: '#707C89' },
    { n: '02', title: 'UNSAFE VIOLATION', rule: '> 50 km/h globally\n> 30 km/h in a zone', explain: 'Exceeds the configured limit', color: C.red },
    { n: '03', title: 'CAUTIOUS CONTROL', rule: 'Brake > 0.15 OR\ndecel. < −1.2 m/s² OR\nspeed < 20 km/h', explain: 'Braking, slowing, or creeping', color: C.rose },
    { n: '04', title: 'NORMAL CRUISING', rule: 'Everything else', explain: 'Steady travel within limits', color: C.navy },
  ];
  states.forEach((st, i) => {
    const y = 183 + i * 112;
    shape(s, 'rect', 66, y, 1148, 96, C.white, '#D8D0C6', 1, 10);
    shape(s, 'rect', 66, y, 12, 96, st.color);
    text(s, st.n, 96, y + 28, 44, 34, { size: 24, color: st.color, bold: true });
    text(s, st.title, 160, y + 19, 290, 28, { size: 20, color: C.charcoal, bold: true });
    text(s, st.rule, 460, y + 15, 330, 66, { size: 18, color: C.plum, bold: true, lineSpacing: 1.05 });
    text(s, st.explain, 824, y + 27, 350, 42, { size: 18, color: C.muted });
  });
  text(s, 'Operational thresholds are prototype definitions—not universal standards for safe driving.', 68, 642, 1115, 22, { size: 15, color: C.muted, italic: true });
  notes(s, 'Priority in code: IdlingDelay first, then UnsafeViolation, then CautiousControl, then NormalCruising. The global limit defaults to 50 km/h and the zone limit to 30 km/h. Braking threshold is 0.15 on a 0–1 scale; deceleration threshold is 1.2 m/s². Source: Assets/WorkSamplingObserver.cs.');
}

// 11 — zone logic
{
  const s = P.slides.add(); header(s, 11, 'Marked zones turn road events into measures', '04  /  EXPERIMENT');
  text(s, 'ZONE APPROACH', 66, 190, 260, 22, { size: 14, color: C.navy, bold: true });
  // An editable top-down road strip.
  shape(s, 'rect', 66, 230, 1148, 204, '#42454B', 'none', 0, 10);
  rule(s, 66, 333, 1214, 333, '#E0E0E0', 3);
  for (let x = 96; x < 1120; x += 98) shape(s, 'rect', x, 329, 46, 7, C.cream);
  // Approach car and zone stripe.
  shape(s, 'roundRect', 202, 295, 104, 54, C.plum, C.white, 1, 12);
  text(s, 'CAR', 220, 309, 66, 22, { size: 14, color: C.white, bold: true, align: 'center' });
  shape(s, 'rect', 842, 230, 17, 204, C.rose);
  text(s, 'ZONE ENTRY', 790, 447, 130, 20, { size: 13, color: C.plum, bold: true, align: 'center' });
  // warning diamonds/poles
  shape(s, 'rect', 808, 251, 20, 20, C.red, 'none', 0, null);
  shape(s, 'rect', 827, 270, 20, 20, '#F2D55C', 'none', 0, null);
  shape(s, 'rect', 1090, 251, 20, 20, C.red, 'none', 0, null);
  shape(s, 'rect', 1109, 270, 20, 20, '#F2D55C', 'none', 0, null);
  text(s, 'Record at entry', 66, 497, 302, 28, { size: 23, color: C.charcoal, bold: true });
  text(s, 'entry speed  ·  maximum zone speed  ·  applicable limit  ·  compliant yes/no', 66, 536, 1088, 36, { size: 20, color: C.muted });
  shape(s, 'rect', 66, 590, 1148, 50, '#E9E4EC', 'none', 0, 10);
  text(s, 'Default zone limit: 30 km/h  ·  compliance threshold includes +2 km/h tolerance  ·  zones are hazard markers, not functioning traffic lights', 83, 604, 1110, 25, { size: 16, color: C.plum, bold: true });
  notes(s, 'The manager can load saved zone locations or let the operator record a zone while stopped from editing. Each zone uses a trigger and logs entry speed, maximum speed, limit, and compliance. The marker is a project-specific hazard/signal zone; a timed traffic-light simulation is not currently implemented. Sources: Assets/ExperimentManager.cs; Assets/SignalZone.cs.');
}

// 12 — data screenshot
{
  const s = P.slides.add(); header(s, 12, 'The observation becomes a row of data', '05  /  EVIDENCE');
  const bytes = await imageBytes('sample-export.png');
  s.images.add({ blob: bytes, contentType: 'image/png', alt: 'Spreadsheet screenshot showing exported Clear and Fog sample rows and driver measures', fit: 'cover', position: { left: 66, top: 190, width: 730, height: 410 }, geometry: 'roundRect', borderRadius: 12 });
  label(s, 'SAMPLE FIELDS', 838, 198, 290, C.navy);
  addBullets(s, [
    'Participant + condition',
    'Sample number + elapsed time',
    'Speed + acceleration',
    'Brake + throttle inputs',
    'Work state + limit + zone flag',
  ], 838, 232, 364, 18, 55, C.charcoal, C.plum);
  shape(s, 'rect', 838, 525, 364, 74, C.plum, 'none', 0, 10);
  text(s, 'EXPORT SET', 856, 539, 145, 18, { size: 13, color: C.rose, bold: true });
  text(s, 'samples · zones · summary · Clear/Fog comparison', 856, 565, 328, 26, { size: 17, color: C.white, bold: true });
  notes(s, 'The spreadsheet image was supplied by the team and illustrates the export format. It contains per-sample data; it should not be interpreted as a balanced study. CSVExporter creates samples.csv, zones.csv, summary.csv and a comparison.md for a session. Sources: team-provided CSV screenshot; Assets/CSVExporter.cs.');
}

// 13 — data quality evidence
{
  const s = P.slides.add(); header(s, 13, 'The export pipeline works; this run is not inferential', '05  /  EVIDENCE', 'A real project export is useful for validating fields and identifying data-quality work.');
  text(s, 'P01 sample counts in the saved comparison', 66, 186, 600, 30, { size: 24, color: C.charcoal, bold: true });
  text(s, 'Clear', 86, 264, 116, 28, { size: 19, color: C.charcoal, bold: true });
  shape(s, 'rect', 208, 263, 14, 30, C.navy, 'none', 0, 5);
  text(s, '1', 234, 264, 40, 28, { size: 19, color: C.charcoal, bold: true });
  text(s, 'Fog', 86, 363, 116, 28, { size: 19, color: C.charcoal, bold: true });
  shape(s, 'rect', 208, 362, 390, 30, C.plum, 'none', 0, 5);
  text(s, '35', 610, 363, 50, 28, { size: 19, color: C.charcoal, bold: true });
  text(s, 'Recorded samples (count)', 208, 422, 420, 24, { size: 14, color: C.muted });
  rule(s, 208, 414, 598, 414, '#D9D2C8', 1);
  shape(s, 'rect', 716, 194, 498, 126, '#E9E4EC', 'none', 0, 12);
  text(s, '1 CLEAR  vs  35 FOG', 742, 215, 445, 34, { size: 27, color: C.plum, bold: true });
  text(s, 'The condition counts are too imbalanced for a meaningful comparison.', 742, 257, 430, 47, { size: 18, color: C.charcoal, bold: true });
  shape(s, 'rect', 716, 344, 498, 126, C.white, '#D8D0C6', 1, 12);
  text(s, 'ZONE OUTCOMES', 742, 364, 260, 22, { size: 14, color: C.navy, bold: true });
  text(s, 'The comparison has no zone-compliance rows; the current sample contains no zone passes.', 742, 398, 430, 50, { size: 18, color: C.charcoal, bold: true });
  shape(s, 'rect', 716, 494, 498, 126, C.charcoal, 'none', 0, 12);
  text(s, 'DATA-QUALITY ACTION', 742, 514, 300, 21, { size: 14, color: C.rose, bold: true });
  text(s, 'A scene screenshot reports 147 loaded zones. Verify and de-duplicate the route file before measuring compliance.', 742, 546, 430, 56, { size: 17, color: C.white, bold: true });
  notes(s, 'These are actual counts from Exports/WorkSampling_P01_20261111_191557_comparison.md: Clear N=1 and Fog N=35. The zones CSV has no result rows, while a supplied scene screenshot shows 147 zone definitions loaded. Therefore the P values in the generated comparison must not be used as evidence of a condition effect. Treat this as a data-pipeline and configuration validation case. Source: project Exports comparison/CSV files and team screenshots.');
}

// 14 — applications and future work
{
  const s = P.slides.add(); header(s, 14, 'Current capability and possible extensions', '06  /  APPLICATION');
  shape(s, 'rect', 66, 185, 520, 412, C.white, '#D8D0C6', 1, 12);
  label(s, 'IN THE CURRENT PROJECT', 90, 207, 350, C.navy);
  addBullets(s, [
    'Urban city scene and connected road network',
    'SUV/JPickup and Sport Coupe configurations',
    'Clear versus Fog visibility runs',
    'Configurable warning / hazard zones',
    'Keyboard and Meta Quest 3 inputs',
    'Random work-state samples and CSV export',
  ], 90, 250, 460, 19, 52, C.charcoal, C.plum);
  shape(s, 'rect', 690, 185, 524, 412, '#E9E4EC', '#D8D0C6', 1, 12);
  label(s, 'POTENTIAL EXTENSIONS', 714, 207, 350, C.plum);
  addBullets(s, [
    'Compare road geometry and intersection layouts',
    'Add timed traffic signals and scripted traffic',
    'Extend weather to rain, night or glare',
    'Study demanding routes for truck or emergency drivers',
    'Build scenario practice for industrial or accident-prone routes',
  ], 714, 250, 464, 18, 61, C.charcoal, C.plum);
  text(s, 'Extensions are research directions, not claims about the current demo.', 714, 560, 456, 25, { size: 15, color: C.plum, bold: true, italic: true });
  notes(s, 'Present potential uses carefully. The current code uses fog and marked zones; timed signals, traffic, rain, night and training validation are future work. The simulator could be adapted to study training scenarios for professional drivers but is not a certified driver-training system.');
}

// 15 — limits and roadmap
{
  const s = P.slides.add(); header(s, 15, 'What the prototype can—and cannot—support', '06  /  APPLICATION');
  text(s, 'Current limits', 66, 188, 440, 32, { size: 26, color: C.plum, bold: true });
  addBullets(s, [
    'One test participant export; Clear/Fog samples are imbalanced',
    'Zone definitions need route-level review before compliance analysis',
    'State labels depend on chosen speed, brake and deceleration thresholds',
    'Short simulated runs do not establish transfer to real-road safety',
    'No gaze tracking or subjective workload survey is currently captured',
  ], 66, 233, 520, 19, 67, C.charcoal, C.red);
  shape(s, 'rect', 650, 186, 564, 405, C.charcoal, 'none', 0, 12);
  label(s, 'NEXT ITERATION', 676, 211, 280, C.rose);
  const steps = [
    ['1', 'Clean the route zones; verify each trigger position'],
    ['2', 'Collect balanced Clear/Fog pilot runs across participants'],
    ['3', 'Review threshold definitions with the course team'],
    ['4', 'Add controlled traffic / signal scenarios as a new module'],
    ['5', 'Evaluate usability and presence before training claims'],
  ];
  steps.forEach((st, i) => {
    const yy = 253 + i * 62;
    shape(s, 'ellipse', 676, yy, 34, 34, C.rose);
    text(s, st[0], 676, yy + 7, 34, 18, { size: 16, bold: true, color: C.plum, align: 'center' });
    text(s, st[1], 728, yy + 2, 440, 48, { size: 18, color: C.white, bold: true });
  });
  text(s, 'Ethics: use consented participants, de-identify exports and keep the simulator separate from live driving.', 68, 615, 1110, 27, { size: 16, color: C.muted, italic: true });
  notes(s, 'Highlight that work samples are not independent time-series observations and that 1 versus 35 samples cannot support a condition comparison. The code includes the same route condition workflow, but route-zone duplicates and missing zone results must be resolved before testing compliance. Sources: saved project export; Assets/WorkSamplingObserver.cs; Assets/ExperimentManager.cs.');
}

// 16 — conclusion and demo
{
  const s = P.slides.add(); s.background.fill = C.plum;
  label(s, 'TAKEAWAY', 68, 48, 220, C.rose);
  text(s, 'A simulator can make a driving work system observable.', 68, 100, 1020, 94, { size: 46, color: C.white, bold: true, lineSpacing: 1.02 });
  text(s, 'This prototype joins VPP vehicle response, keyboard / Quest control, a city road environment, Clear–Fog runs, randomized work sampling, hazard-zone events and file-based analysis.', 70, 232, 1080, 80, { size: 23, color: '#E9E1E8', lineSpacing: 1.14 });
  rule(s, 70, 360, 1180, 360, '#896C83', 1);
  text(s, 'DEMO SEQUENCE', 70, 390, 260, 21, { size: 14, color: C.rose, bold: true });
  const demos = [
    ['01', 'Choose SUV or Sport Coupe'], ['02', 'Show controls + dashboard'], ['03', 'Run Clear, then Fog'], ['04', 'Approach a marked zone'], ['05', 'Open exported CSVs'],
  ];
  demos.forEach((d, i) => {
    const x = 70 + i * 224;
    text(s, d[0], x, 436, 48, 30, { size: 24, color: C.rose, bold: true });
    text(s, d[1], x, 475, 198, 54, { size: 18, color: C.white, bold: true });
  });
  text(s, 'Thank you  /  Questions', 70, 578, 720, 44, { size: 27, color: C.white, bold: true });
  footer(s, 16, true);
  notes(s, 'Close with the prototype’s contribution: it ties an interactive vehicle task to repeatable conditions and a defined data path. Then follow the five-step demo sequence. Do not present the current P01 export as a validated experimental result.');
}

// 17 — appendix controls
{
  const s = P.slides.add(); header(s, 17, 'Appendix  /  control quick reference', 'APPENDIX');
  const vals = [
    ['Function', 'Keyboard', 'Meta Quest 3'],
    ['Steer', 'WASD / arrows', 'Left stick X'],
    ['Throttle / brake', 'Up / down or W / S', 'Right trigger / left trigger'],
    ['Handbrake / clutch', 'Space / Shift', 'Right grip / left grip'],
    ['Manual shift', 'Tab / Caps Lock', 'Right stick up / down'],
    ['Ignition', 'K (per scene help)', 'A tap / hold'],
    ['Reverse / camera / reset', 'R / C or F1–F4 / Enter', 'B / X / Y'],
    ['Recenter', '—', 'Right stick click'],
    ['Experiment modifier', '1 Clear / 2 Fog / 3 stop', 'Hold left click: A clear, B fog, X end'],
    ['Zone / undo / export', 'Z / Backspace / F9', 'Modifier + Y / right down / right click'],
  ];
  addTable(s, vals, 66, 178, 1148, 430, { size: 17, firstColumnBold: true });
  text(s, 'Scene help panels are the final authority for legacy keyboard bindings; XR controls are mapped by Quest3DrivingInput.', 68, 632, 1110, 28, { size: 15, color: C.muted, italic: true });
  notes(s, 'Use as a reference during the live demo. Keyboard bindings are partly inherited from VPP help text and can differ by scene configuration. Quest inputs and experiment modifier mappings are defined in the project scripts. Sources: Assets/Quest3DrivingInput.cs; Assets/ExperimentManager.cs; scene help panels.');
}

// 18 — appendix data dictionary and sources
{
  const s = P.slides.add(); header(s, 18, 'Appendix  /  data fields and project sources', 'APPENDIX');
  const vals = [
    ['Export', 'Contents'],
    ['*_samples.csv', 'Participant, condition, time, speed, acceleration, brake, throttle, work state, limit and zone flag'],
    ['*_zones.csv', 'Entry time/speed, maximum speed, limit and compliance outcome'],
    ['*_summary.csv', 'Work-state proportions and interval estimates by condition'],
    ['*_comparison.md', 'Clear/Fog summary table; do not interpret unbalanced pilot counts'],
  ];
  addTable(s, vals, 66, 182, 690, 365, { size: 16, firstColumnBold: true });
  label(s, 'PROJECT SOURCES', 800, 190, 330, C.navy);
  const src = [
    'EXPERIMENT_GUIDE.md — protocol and operational definitions',
    'Assets/ExperimentManager.cs — run and zone workflow',
    'Assets/WorkSamplingObserver.cs — random sample + state classifier',
    'Assets/CSVExporter.cs — exported fields and analysis summary',
    'Assets/Quest3DrivingInput.cs — Touch Plus input mapping',
    'Team screenshots — city scene, cockpit and sample export',
  ];
  addBullets(s, src, 800, 230, 394, 18, 53, C.charcoal, C.plum);
  shape(s, 'rect', 66, 581, 1148, 61, '#E9E4EC', 'none', 0, 10);
  text(s, 'Claim boundary: implemented Clear/Fog work sampling and hazard zones; timed signals, traffic and professional-driver training validation are future extensions.', 85, 598, 1104, 30, { size: 16, color: C.plum, bold: true });
  notes(s, 'Use this slide to answer questions about exported data and implementation locations. Sources are local project files and team-provided screenshots. Future-application claims are deliberately separated from current features.');
}

if (P.slides.items.length !== 18) throw new Error(`Expected 18 slides, found ${P.slides.items.length}`);

await fs.mkdir(BUILD, { recursive: true });
await fs.mkdir(OUTPUT, { recursive: true });
for (const [i, slide] of P.slides.items.entries()) {
  const png = await P.export({ slide, format: 'png', scale: 1 });
  await fs.writeFile(path.join(BUILD, `slide-${String(i + 1).padStart(2, '0')}.png`), new Uint8Array(await png.arrayBuffer()));
}
const montage = await P.export({ format: 'webp', montage: true });
await fs.writeFile(path.join(BUILD, 'montage.webp'), new Uint8Array(await montage.arrayBuffer()));

const candidatePath = path.join(BUILD, 'candidate.pptx');
await (await PresentationFile.exportPptx(P)).save(candidatePath);
const result = await finalizePresentation({
  workspaceDir: ROOT,
  candidatePath,
  finalPath: FINAL_PPTX,
  pythonExecutable: 'C:\\Program Files\\WindowsApps\\OpenAI.CodexPrimaryRuntime.v26-1110-973-0_26.1110.973.0_x64__3k8sg7r9htsxt\\dependencies\\python\\python.exe',
  integrityValidatorPath: 'C:\\Users\\naksh\\.codex\\plugins\\cache\\openai-primary-runtime\\presentations\\26.1110.11613\\skills\\presentations\\container_tools\\inspect_presentation_package_integrity.py',
  layoutValidatorPath: 'C:\\Users\\naksh\\.codex\\plugins\\cache\\openai-primary-runtime\\presentations\\26.1110.11613\\skills\\presentations\\container_tools\\inspect_presentation_layout_geometry.py',
  layoutArgs: ['--expected-slide-size-emu', '12192000,6858000', '--validate-heading-fit', '--require-native-table-slide', '7', '--require-native-table-slide', '17', '--require-native-table-slide', '18'],
  requiredNativeTableOwnerSlides: [7, 17, 18],
  fontPolicy: { basis: 'design', families: [FONT] },
  verifyArtifactToolImport: true,
  receiptPath: path.join(BUILD, 'validation-final-v3.json'),
});
console.log(JSON.stringify({ finalPath: FINAL_PPTX, slides: P.slides.items.length, result }, null, 2));
