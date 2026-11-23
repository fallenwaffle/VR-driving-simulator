#!/usr/bin/env python3
"""
Work-sampling analysis: Clear vs Fog.

Usage:
    python analyze_work_sampling.py                 # reads ../Exports/*_samples.csv and *_zones.csv
    python analyze_work_sampling.py path/to/folder  # or any folder with the exported CSVs

Outputs (written next to the data):
    analysis_comparison.csv   proportions, 95% CI (E = 1.96*sqrt(p(1-p)/N)), z-test p-values
    analysis_comparison.md    slide-ready table
    analysis_chart.png        grouped bar chart with CI error bars
Needs: pandas, numpy, scipy, matplotlib
"""
import glob, math, os, sys
import numpy as np
import pandas as pd
from scipy import stats

Z = 1.96
STATES = [
    ("NormalCruising",  "1. Normal Cruising (Productive)"),
    ("CautiousControl", "2. Cautious Slowing / Control"),
    ("IdlingDelay",     "3. Idling / Stopped (Delay)"),
    ("UnsafeViolation", "4. Speeding / Violation"),
]


def half_width(p, n):
    return Z * math.sqrt(p * (1 - p) / n) if n else float("nan")


def wilson(k, n):
    if n == 0:
        return float("nan"), float("nan")
    p = k / n
    d = 1 + Z * Z / n
    c = (p + Z * Z / (2 * n)) / d
    m = Z * math.sqrt(p * (1 - p) / n + Z * Z / (4 * n * n)) / d
    return max(0, c - m), min(1, c + m)


def two_prop_p(k1, n1, k2, n2):
    if n1 == 0 or n2 == 0:
        return float("nan")
    pp = (k1 + k2) / (n1 + n2)
    se = math.sqrt(pp * (1 - pp) * (1 / n1 + 1 / n2))
    if se == 0:
        return 1.0
    z = (k1 / n1 - k2 / n2) / se
    return 2 * (1 - stats.norm.cdf(abs(z)))


def load(folder, suffix):
    files = sorted(glob.glob(os.path.join(folder, f"*_{suffix}.csv")))
    if not files:
        sys.exit(f"No *_{suffix}.csv files found in {folder}")
    # exporter rewrites the same session files on every export, so just read them all
    return pd.concat([pd.read_csv(f).assign(SourceFile=os.path.basename(f)) for f in files], ignore_index=True)


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    folder = sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, "..", "Exports")
    samples = load(folder, "samples")
    zones = load(folder, "zones")

    s = {c: samples[samples.Condition == c] for c in ("Clear", "Fog")}
    z = {c: zones[zones.Condition == c] for c in ("Clear", "Fog")}
    n = {c: len(s[c]) for c in s}

    rows = []
    for key, label in STATES:
        row = {"Metric": label}
        for c in ("Clear", "Fog"):
            k = int((s[c].State == key).sum())
            p = k / n[c] if n[c] else float("nan")
            row[f"{c}_count"], row[f"{c}_N"], row[f"{c}_p"] = k, n[c], p
            row[f"{c}_E"] = half_width(p, n[c]) if n[c] else float("nan")
        row["Change_pp"] = (row["Fog_p"] - row["Clear_p"]) * 100
        row["z_test_p"] = two_prop_p(row["Fog_count"], n["Fog"], row["Clear_count"], n["Clear"])
        rows.append(row)

    # compliance
    crow = {"Metric": "Signal compliance"}
    for c in ("Clear", "Fog"):
        t = len(z[c])
        k = int(z[c].Compliant.astype(str).str.lower().eq("true").sum())
        crow[f"{c}_count"], crow[f"{c}_N"] = k, t
        crow[f"{c}_p"] = k / t if t else float("nan")
        lo, hi = wilson(k, t)
        crow[f"{c}_E"] = (hi - lo) / 2
    crow["Change_pp"] = (crow["Fog_p"] - crow["Clear_p"]) * 100
    if crow["Clear_N"] and crow["Fog_N"]:
        table = [[crow["Clear_count"], crow["Clear_N"] - crow["Clear_count"]],
                 [crow["Fog_count"], crow["Fog_N"] - crow["Fog_count"]]]
        crow["z_test_p"] = stats.fisher_exact(table)[1]   # exact test: small number of zones
    else:
        crow["z_test_p"] = float("nan")
    rows.append(crow)

    df = pd.DataFrame(rows)
    df.to_csv(os.path.join(folder, "analysis_comparison.csv"), index=False)

    # overall chi-square on the 4x2 state table
    chi_txt = ""
    if n["Clear"] and n["Fog"]:
        tab = np.array([[r["Clear_count"], r["Fog_count"]] for r in rows[:4]])
        tab = tab[tab.sum(axis=1) > 0]
        chi2, pchi, dof, _ = stats.chi2_contingency(tab)
        chi_txt = f"Chi-square test of state distribution (Clear vs Fog): chi2={chi2:.2f}, dof={dof}, p={pchi:.4f}"

    def fmt(r, c, last=False):
        if not r[f"{c}_N"]:
            return "-"
        if last:
            return f"{int(r[f'{c}_count'])}/{int(r[f'{c}_N'])} ({r[f'{c}_p']*100:.1f}%)"
        return f"{r[f'{c}_p']*100:.1f}% ({int(r[f'{c}_count'])}) +/-{r[f'{c}_E']*100:.1f}"

    md = [f"| Work state / metric | Run 1: Clear (N={n['Clear']}) | Run 2: Fog (N={n['Fog']}) | Change (pp) | p-value |",
          "|---|---|---|---|---|"]
    for i, r in enumerate(rows):
        last = i == len(rows) - 1
        name = f"**{r['Metric']}**" if last else r["Metric"]
        md.append(f"| {name} | {fmt(r,'Clear',last)} | {fmt(r,'Fog',last)} | {r['Change_pp']:+.1f} | {r['z_test_p']:.3f} |")
    md_text = "\n".join(md) + "\n\n" + chi_txt + "\n(state rows: two-proportion z-test; compliance row: Fisher exact test)\n"
    open(os.path.join(folder, "analysis_comparison.md"), "w", encoding="utf-8").write(md_text)
    print(md_text)

    # chart
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    labels = [r["Metric"].split(". ", 1)[-1] for r in rows]
    x = np.arange(len(rows))
    w = 0.38
    fig, ax = plt.subplots(figsize=(10, 5))
    for off, c, col in ((-w / 2, "Clear", "#4c9be8"), (w / 2, "Fog", "#8a8f98")):
        ax.bar(x + off, [r[f"{c}_p"] * 100 for r in rows], w, yerr=[r[f"{c}_E"] * 100 for r in rows],
               capsize=4, label=f"{c} (N={n[c]})", color=col)
    ax.set_xticks(x)
    ax.set_xticklabels(labels, rotation=12, ha="right")
    ax.set_ylabel("% of samples / zones (95% CI)")
    ax.set_title("Driver time allocation and signal compliance: Clear vs Fog")
    ax.legend()
    fig.tight_layout()
    out = os.path.join(folder, "analysis_chart.png")
    fig.savefig(out, dpi=160)
    print("Saved:", out)


if __name__ == "__main__":
    main()
