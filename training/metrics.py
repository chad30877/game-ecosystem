"""共用採食、繁殖及戰鬥事件的配置指標定義。"""


def accumulate(events, individuals):
    for event in events:
        actor = event["parent"] if event["kind"] == "born" else event["individual"]
        metrics = individuals.get(actor)
        if metrics is not None:
            if event["kind"] == "ate":
                metrics["foodEnergy"] += event["amount"]
            elif event["kind"] == "born":
                metrics["births"] += 1
            elif event["rawAmount"] > 0 and event["kind"] != "shield_blocked":
                metrics["damageDealt"] += event["amount"]
        target = individuals.get(event["target"])
        if target is not None and event["rawAmount"] > 0 and event["kind"] != "shield_blocked":
            target["damageTaken"] += event["amount"]
