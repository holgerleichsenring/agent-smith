#!/usr/bin/env python3
"""
Refresh the bundled model price list (src/backend/AgentSmith.Application/Resources/model-prices.json)
from the public LiteLLM list. A release step, like the skills pin: the runtime never fetches.

Normalisation happens HERE, so the runtime only reads:
  - keeps entries whose mode is chat or responses and whose input cost is a number
    (sample_spec and every non-model row drop out);
  - converts per-token floats to per-million values rounded to 6 places;
  - indexes a provider-prefixed key (azure/gpt-5.6) under its bare name (gpt-5.6) only
    when no unprefixed entry of that name exists and every prefixed entry of it agrees
    on price; an ambiguous bare name stays unindexed.

Usage: python3 tools/update-model-prices.py [--source <url-or-path>]
"""
import argparse
import datetime
import json
import os
import urllib.request
from decimal import Decimal, ROUND_HALF_UP

SOURCE_URL = "https://raw.githubusercontent.com/BerriAI/litellm/main/model_prices_and_context_window.json"
KEPT_MODES = {"chat", "responses"}
TARGET = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..",
                      "src", "backend", "AgentSmith.Application", "Resources", "model-prices.json")


def is_number(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool)


def per_million(per_token):
    scaled = (Decimal(repr(per_token)) * 1_000_000).quantize(Decimal("0.000001"), rounding=ROUND_HALF_UP)
    return float(scaled.normalize())


def normalise(row):
    price = {
        "inputPerMillion": per_million(row["input_cost_per_token"]),
        "outputPerMillion": per_million(row["output_cost_per_token"]) if is_number(row.get("output_cost_per_token")) else 0.0,
    }
    if is_number(row.get("cache_read_input_token_cost")):
        price["cacheReadPerMillion"] = per_million(row["cache_read_input_token_cost"])
    if is_number(row.get("max_input_tokens")):
        price["contextWindowTokens"] = int(row["max_input_tokens"])
    price["provider"] = str(row.get("litellm_provider") or "")
    return price


def keep(key, row):
    return (key != "sample_spec" and isinstance(row, dict)
            and row.get("mode") in KEPT_MODES and is_number(row.get("input_cost_per_token")))


def price_key(price):
    return (price["inputPerMillion"], price["outputPerMillion"], price.get("cacheReadPerMillion"))


def bare_aliases(models):
    # Case-insensitive throughout, because the runtime looks names up ignoring case.
    unprefixed = {key.lower() for key in models}
    by_bare = {}
    for key in sorted(models):
        if "/" in key:
            by_bare.setdefault(key.rsplit("/", 1)[1].lower(), []).append(key)
    aliases = {}
    for bare, keys in sorted(by_bare.items()):
        if bare and bare not in unprefixed and len({price_key(models[k]) for k in keys}) == 1:
            aliases[keys[0].rsplit("/", 1)[1]] = keys[0]
    return aliases


def load(source):
    if os.path.exists(source):
        with open(source, encoding="utf-8") as fp:
            return json.load(fp)
    with urllib.request.urlopen(source, timeout=60) as response:
        return json.load(response)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--source", default=SOURCE_URL)
    args = parser.parse_args()
    raw = load(args.source)
    models = {key: normalise(row) for key, row in sorted(raw.items()) if keep(key, row)}
    snapshot = {
        "source": SOURCE_URL,
        "fetchedAt": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "models": models,
        "aliases": bare_aliases(models),
    }
    with open(TARGET, "w", encoding="utf-8") as fp:
        json.dump(snapshot, fp, indent=1, sort_keys=False)
        fp.write("\n")
    print(f"wrote {len(models)} models and {len(snapshot['aliases'])} bare-name aliases to {os.path.normpath(TARGET)}")


if __name__ == "__main__":
    main()
