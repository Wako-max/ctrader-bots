CONTRACTION_ENTER = 0.82
CONTRACTION_EXIT = 0.95
EXPANSION_EXIT = 1.05
EXPANSION_ENTER = 1.20


def classify(previous_state, ratio):
    if previous_state == "EXPANSION":
        if ratio >= EXPANSION_EXIT:
            return "EXPANSION"
        if ratio <= CONTRACTION_ENTER:
            return "CONTRACTION"
        return "NEUTRAL"

    if previous_state == "CONTRACTION":
        if ratio <= CONTRACTION_EXIT:
            return "CONTRACTION"
        if ratio >= EXPANSION_ENTER:
            return "EXPANSION"
        return "NEUTRAL"

    if ratio >= EXPANSION_ENTER:
        return "EXPANSION"
    if ratio <= CONTRACTION_ENTER:
        return "CONTRACTION"
    return "NEUTRAL"


def run(sequence, initial="NEUTRAL"):
    state = initial
    result = []
    for ratio in sequence:
        state = classify(state, ratio)
        result.append(state)
    return result


def main():
    cases = [
        (
            "neutral noise stays neutral",
            [0.97, 1.02, 0.90, 1.10],
            ["NEUTRAL", "NEUTRAL", "NEUTRAL", "NEUTRAL"],
        ),
        (
            "expansion enters and survives pullback",
            [1.21, 1.12, 1.06],
            ["EXPANSION", "EXPANSION", "EXPANSION"],
        ),
        (
            "expansion exits only below exit threshold",
            [1.21, 1.06, 1.04],
            ["EXPANSION", "EXPANSION", "NEUTRAL"],
        ),
        (
            "contraction enters and survives bounce",
            [0.81, 0.90, 0.94],
            ["CONTRACTION", "CONTRACTION", "CONTRACTION"],
        ),
        (
            "contraction exits only above exit threshold",
            [0.81, 0.94, 0.96],
            ["CONTRACTION", "CONTRACTION", "NEUTRAL"],
        ),
        (
            "strong reversal can flip directly",
            [1.25, 0.80],
            ["EXPANSION", "CONTRACTION"],
        ),
    ]

    for name, sequence, expected in cases:
        actual = run(sequence)
        assert actual == expected, (name, actual, expected)
        print("[PASS]", name, actual)

    print(f"{len(cases)} passed, 0 failed")


if __name__ == "__main__":
    main()
