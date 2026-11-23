"""Compare Clear vs Fog runs from the summary CSVs exported by Unity.
Usage: python compare_runs.py summary_Clear_*.csv summary_Fog_*.csv
Needs: pandas, numpy, scipy, matplotlib
"""
import sys
import numpy as np
import pandas as pd
from scipy.stats import norm
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

STATES = [("Cruising", "nCruising"), ("Cautious", "nCautious"),
          ("Idle", "nIdle"), ("Violation", "nViolation")]

def ci(k, n):
    p = k / n if n else float("nan")
    se = np.sqrt(p * (1 - p) / n) if n else float("nan")
    return p, max(0.0, p - 1.96 * se), min(1.0, p + 1.96 * se)

def z_test(k1, n1, k2, n2):
    if min(n1, n2) == 0:
        return float("nan"), float("nan")
    p1, p2, p = k1 / n1, k2 / n2, (k1 + k2) / (n1 + n2)
    se = np.sqrt(p * (1 - p) * (1 / n1 + 1 / n2))
    if se == 0:
        return 0.0, 1.0
    z = (p1 - p2) / se
    return z, 2 * (1 - norm.cdf(abs(z)))

def main(clear_csv, fog_csv):
    a, b = pd.read_csv(clear_csv).iloc[0], pd.read_csv(fog_csv).iloc[0]
    rows = []
    for name, col in STATES:
        k1, n1, k2, n2 = int(a[col]), int(a["N"]), int(b[col]), int(b["N"])
        p1, l1, h1 = ci(k1, n1); p2, l2, h2 = ci(k2, n2)
        z, pv = z_test(k1, n1, k2, n2)
        warn = "small sample" if min(n1 * p1, n2 * p2, n1 * (1 - p1), n2 * (1 - p2)) < 5 else ""
        rows.append([name, p1, l1, h1, p2, l2, h2, z, pv, warn])
    t1, f1, t2, f2 = int(a["TotalSignals"]), int(a["SignalsFollowed"]), int(b["TotalSignals"]), int(b["SignalsFollowed"])
    p1, l1, h1 = ci(f1, t1); p2, l2, h2 = ci(f2, t2)
    z, pv = z_test(f1, t1, f2, t2)
    warn = "small sample" if min(t1, t2) < 30 else ""
    rows.append(["Signal compliance", p1, l1, h1, p2, l2, h2, z, pv, warn])

    df = pd.DataFrame(rows, columns=["Metric", "Clear_p", "Clear_lo", "Clear_hi",
                                     "Fog_p", "Fog_lo", "Fog_hi", "z", "p_value", "Note"])
    if int(a["N"]) < 30 or int(b["N"]) < 30:
        print("WARNING: fewer than 30 samples in a run; intervals are unreliable.")
    pd.set_option("display.width", 200)
    print(df.round(4).to_string(index=False))
    df.round(4).to_csv("comparison_table.csv", index=False)

    x = np.arange(len(df)); w = 0.38
    fig, ax = plt.subplots(figsize=(9, 5))
    for off, lab, p, lo, hi in [(-w / 2, "Clear", "Clear_p", "Clear_lo", "Clear_hi"),
                                (w / 2, "Fog", "Fog_p", "Fog_lo", "Fog_hi")]:
        err = [df[p] - df[lo], df[hi] - df[p]]
        ax.bar(x + off, df[p] * 100, w, yerr=np.array(err) * 100, capsize=4, label=lab)
    ax.set_xticks(x); ax.set_xticklabels(df["Metric"], rotation=15)
    ax.set_ylabel("Percent (%)"); ax.set_title("Clear vs Fog (95% CI)"); ax.legend()
    plt.tight_layout(); plt.savefig("comparison_chart.png", dpi=150)
    print("Saved comparison_table.csv and comparison_chart.png")

if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    main(sys.argv[1], sys.argv[2])
