"""彙整訓練輪次及各物種技能配置的外部表現。"""
import argparse
import json
from pathlib import Path

from metrics import accumulate


def summarize(world_log: Path):
    rounds = []
    configurations = {}
    individuals = {}
    individual_metrics = {}
    last_state = None
    world_steps = 0
    life_records = {}
    with world_log.open(encoding="utf-8-sig") as source:
        for line in source:
            record = json.loads(line)
            if "state" not in record:
                continue
            state = record["state"]
            last_state = state
            for individual in state.get("individuals", []):
                if "ledger" in individual:
                    life_records[individual["individual"]] = {"individual": individual["individual"], "species": individual["species"],
                        "initialAdult": individual["initialAdult"], "observedSeconds": individual["observedSeconds"],
                        "age": individual["age"], "loadout": individual["loadout"], "censored": True, "ledger": individual["ledger"],
                        "categories": {key: individual[key] for key in ("basalSpent", "activityStaminaSpent", "skillManaSpent", "reproductionSpent", "foodReceived", "automaticRecovered", "nutritionRecovered", "nutritionSpent", "combatDamageTaken", "starvationDamageTaken", "agingHealthLost")}}
            for death in state.get("deaths", []):
                life_records[death["individual"]] = {**life_records.get(death["individual"], {}), **death, "censored": False}
                if "ledger" in death:
                    life_records[death["individual"]]["categories"] = {key: death[key] for key in ("basalSpent", "activityStaminaSpent", "skillManaSpent", "reproductionSpent", "foodReceived", "automaticRecovered", "nutritionRecovered", "nutritionSpent", "agingHealthLost")}
                    life_records[death["individual"]]["categories"].update(combatDamageTaken=death["combatDamage"], starvationDamageTaken=death["starvationDamage"])
            world_steps = max(world_steps, record["totalSteps"])
            if record["kind"] == "agent_joined":
                individual = record["individual"]
                key = (individual["species"], tuple(sorted(individual["loadout"])))
                metrics = configurations.setdefault(key, {
                    "species": key[0], "loadout": list(key[1]), "individuals": 0,
                    "survivalSeconds": 0.0, "foodEnergy": 0.0, "births": 0,
                    "damageDealt": 0.0, "damageTaken": 0.0, "deaths": 0,
                    "incompleteLives": 0})
                metrics["individuals"] += 1
                individuals[individual["individual"]] = (metrics, state["time"])
                individual_metrics[individual["individual"]] = metrics
            if record["kind"] == "step":
                for individual in state["individuals"]:
                    if individual["individual"] in individuals:
                        metrics, age = individuals[individual["individual"]]
                        metrics["survivalSeconds"] += max(0, state["time"] - age)
                        individuals[individual["individual"]] = (metrics, state["time"])
                accumulate(state["events"], individual_metrics)
            if record["kind"] == "agent_died":
                individual = record["individual"]
                metrics, age = individuals.pop(individual["individual"])
                metrics["survivalSeconds"] += max(0, state["time"] - age)
                metrics["deaths"] += 1
            if record["kind"] == "episode_end":
                rounds.append({"episode": state["episode"], "steps": state["step"],
                               "reason": state["endReason"], "species": state["statistics"]})
                for metrics, _ in individuals.values():
                    metrics["incompleteLives"] += 1
                individuals.clear()
                individual_metrics.clear()
    for metrics, _ in individuals.values():
        metrics["incompleteLives"] += 1
    for metrics in configurations.values():
        metrics["survivalSeconds"] = sum(life["observedSeconds"] for life in life_records.values()
            if life["species"] == metrics["species"] and sorted(life.get("loadout", [])) == metrics["loadout"])
        metrics["foodSatiety"] = sum(life.get("categories", {}).get("foodReceived", 0) for life in life_records.values()
            if life["species"] == metrics["species"] and sorted(life.get("loadout", [])) == metrics["loadout"])
    completed = [life["observedSeconds"] for life in life_records.values() if not life["censored"]]
    return {"resourceOrder": ["health", "stamina", "mana", "satiety"], "lives": list(life_records.values()),
            "completedLives": len(completed), "censoredLives": sum(life["censored"] for life in life_records.values()),
            "meanCompletedObservedSeconds": sum(completed) / len(completed) if completed else None,
            "worldSteps": world_steps, "completedRounds": len(rounds), "rounds": rounds,
            "lastEpisode": None if last_state is None else {
                "episode": last_state["episode"], "steps": last_state["step"],
                "reason": last_state["endReason"], "species": last_state["statistics"]},
            "configurations": list(configurations.values())}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("world_log", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    report = summarize(args.world_log)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"worldSteps": report["worldSteps"],
                      "completedRounds": report["completedRounds"]}, ensure_ascii=False))


if __name__ == "__main__":
    main()
