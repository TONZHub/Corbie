"""Read-only JSON bridge from Corbie to the vendored pm2-solver.

Never advances real observations or operates the game. Projections are separate.
"""
import json
import math
import sys
from pm2solver import data as D, sim, planner

NOTICE = "Approximate fan-guide simulation; projected stats are not observed game facts."
VISIBLE_STATS = D.GENERAL_STATS + D.COMBAT_SKILLS + D.GENERAL_SKILLS
CALIBRATED_JOBS = ("housework", "babysitting", "inn", "farm", "church", "restaurant")


def number(value, name, low=0, high=99999999):
    if isinstance(value, bool) or not isinstance(value, (float, int)) or not math.isfinite(value) or not low <= value <= high:
        raise ValueError("Invalid " + name)
    return value


def validate(obj):
    if not isinstance(obj, dict):
        raise ValueError("Stats must be a JSON object.")
    state = obj.get("state", obj)
    if not isinstance(state, dict):
        raise ValueError("state must be an object.")
    required = ("year", "month", "age", "birth_month", "blood", "sign", "gold", "diet", "stats")
    missing = [k for k in required if k not in state]
    if missing:
        raise ValueError("Missing current values: " + ", ".join(missing))
    for key, lo, hi in (("year", 1100, 1299), ("month", 1, 12), ("age", 10, 17), ("birth_month", 1, 12)):
        value = number(state[key], key, lo, hi)
        if int(value) != value:
            raise ValueError(key + " must be an integer.")
    number(state["gold"], "gold")
    if state.get("period", 0) != 0:
        raise ValueError("Supply stats at the start of a month, not a mid-month simulation.")
    if state["sign"] not in D.SIGN_BIRTH_MONTH or state["blood"] not in D.BLOOD_TYPES or state["diet"] not in D.DIETS:
        raise ValueError("Unknown sign, blood type or diet.")
    if not isinstance(state["stats"], dict):
        raise ValueError("stats must be an object.")
    canonical = {}
    for key, value in state["stats"].items():
        name = D.ALIASES.get(key.lower(), key.lower())
        if name not in D.ALL_STATS or name in canonical:
            raise ValueError("Unknown or duplicate stat: " + key)
        canonical[name] = number(value, key, 0, 9999)
    missing = [k for k in VISIBLE_STATS if k not in canonical]
    if missing:
        raise ValueError("Missing observed stats: " + ", ".join(missing))
    for key in ("job_days", "job_success_days", "class_sessions", "rep_bonus", "items"):
        values = state.get(key, {})
        if not isinstance(values, dict):
            raise ValueError(key + " must be an object.")
        allowed = D.JOBS if key.startswith("job_") else D.CLASSES if key == "class_sessions" else D.REPUTATIONS if key == "rep_bonus" else D.ITEMS
        for name, value in values.items():
            if name not in allowed:
                raise ValueError("Unknown " + key + " entry: " + name)
            number(value, key + "." + name)
    clean = {k: state[k] for k in required if k != "stats"}
    clean["stats"] = canonical
    for key in ("version", "period", "allowance_month", "job_days", "job_success_days", "class_sessions", "rep_bonus", "items", "angels_painted"):
        if key in state:
            clean[key] = state[key]
    if "allowance_month" in clean:
        number(clean["allowance_month"], "allowance_month", 1, 12)
    if "version" in clean and clean["version"] != 1:
        raise ValueError("Unsupported stats version.")
    if "angels_painted" in clean and not isinstance(clean["angels_painted"], bool):
        raise ValueError("angels_painted must be true or false.")
    assumptions = ["Missing job experience uses starting pay tiers; unknown class history uses beginner tuition.",
                   "Missing reputation bonuses, hidden stats and items use simulator defaults; they are not observations."]
    return sim.normalize(clean), assumptions


def knowledge(names):
    out = {}
    for raw in names[:20]:
        name = raw.lower().replace(" ", "_")
        name = {"babysitter": "babysitting", "time_off": "free_time", "free_time": "free_time"}.get(name, name)
        entry = D.JOBS.get(name) or D.CLASSES.get(name)
        if entry:
            out[raw] = dict(entry, source=D.SOURCES.get(entry.get("src"), ""))
        elif name == "free_time":
            out[raw] = dict(D.REST["free_time"], source=D.SOURCES["gamefaqs_ray"])
    return out


def survival(start, names):
    allowed = ["job:" + j for j in CALIBRATED_JOBS if j in names and sim.job_available(start, j)] + ["rest:free_time"]
    nodes = [(sim.clone(start), [], [])]
    for period in range(3):
        candidates = []
        for state, actions, infos in nodes:
            for action in allowed:
                nxt, info = sim.apply_period(state, action)
                if nxt is None or (action.startswith("job:") and nxt["stats"]["stress"] > sim.stress_limit(nxt)):
                    continue
                if period == 2:
                    sim.end_month(nxt, pocket_money=False)
                if nxt["gold"] < 0:
                    continue
                # Preserve a living-cost buffer, build weak stamina, then favor low stress.
                st = nxt["stats"]
                score = min(nxt["gold"], 600) / 15 + min(st["constitution"], 100) + min(max(st["faith"], st["morality"]), 80) / 4 - st["stress"] * 2
                candidates.append((score, nxt, actions + [action], infos + [info]))
        if not candidates:
            raise ValueError("No solvent schedule found with calibrated jobs. Inspect costs/health in the game.")
        nodes = [(s, a, i) for _, s, a, i in sorted(candidates, key=lambda x: -x[0])[:32]]
    state, actions, infos = nodes[0]
    return {"goal": "finish alive, any ending", "actions": actions, "periods": infos,
            "projected": {"gold": state["gold"], "stress": state["stats"]["stress"], "constitution": state["stats"]["constitution"]}}


def main(req):
    command = req.get("command")
    if command == "knowledge":
        return {"notice": NOTICE, "knowledge": knowledge(req.get("names", []))}
    state, assumptions = validate(req.get("stats"))
    if command == "validate":
        return {"state": state, "assumptions": assumptions}
    if command != "plan":
        raise ValueError("Unknown solver operation.")
    if state["month"] == 10:
        return {"notice": NOTICE, "status": "festival", "assumptions": assumptions,
                "reason": "October is a festival month; ordinary three-period scheduling advice is withheld."}
    goal = req.get("goal", "survival")
    if goal == "survival":
        plan = survival(state, req.get("jobs", list(CALIBRATED_JOBS)))
    else:
        name, _, _, best, months = planner.plan(state, goal, beam=16, duel_cap=0, pocket_money=False, max_months=1)
        planner.label_months(state, months)
        plan = {"goal": name, "month": months[0], "actions": months[0]["actions"],
                "projected": {"gold": best.s["gold"], "stress": best.s["stats"]["stress"]},
                "note": "One-month lookahead toward this ending; not a demonstrated ending route. Lessons may need menu calibration."}
    return {"notice": NOTICE, "status": "advice", "year": state["year"], "month": state["month"], "assumptions": assumptions, **plan}


if __name__ == "__main__":
    try:
        result = main(json.load(sys.stdin))
        print(json.dumps({"ok": True, **result}, allow_nan=False))
    except Exception as error:
        print(json.dumps({"ok": False, "error": str(error)}))
        sys.exit(1)
