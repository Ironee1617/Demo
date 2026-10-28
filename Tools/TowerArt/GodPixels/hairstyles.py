"""Hairstyles cut from chuchuking's Zeus.

Every 'front' block is a whole head of hair drawn after the face, outline included
('a'), with the ear baked in. The crown reaches row 19 like Zeus's, so every god stands
exactly as tall from boots to crown; the sides follow his hair (x 17..40) and only long
and wavy hair falls further down. Columns start at x=14; the face spans columns 13..22
(x 27..36) and rows 29..40, and rows 33..38 of those columns stay clear for the eyes.
'.' lets the face (or its outline) show through.
Tones: a dark clump line, b shadow, c base, d light, e highlight.
"""
X0 = 14
WIDTH = 28


def _row(*segments):
    """Builds one 28-column row from (column, text) segments."""
    line = ['.'] * WIDTH
    for col, text in segments:
        if col + len(text) > WIDTH:
            raise ValueError('hair row overflows column %d: %s' % (WIDTH - 1, text))
        for i, ch in enumerate(text):
            line[col + i] = ch
    return ''.join(line)


# Rows 33..39 behind the near cheek: hair clumps around Zeus's ear (columns 8..10).
_NAPE = [
    "...aaccbQQaca..........aa...",  # 33
    "....aaaaQqaba..........a....",  # 34
    ".......aQkaba...............",  # 35
    "......acbQQcb...............",  # 36
    "....aacbbcab................",  # 37
    ".....aaaabba................",  # 38
    "........aa..................",  # 39
]

# Zeus's crown rows 19..21: two thin locks and the outline over the top of the head,
# so every head is exactly as tall as his and the solid hair starts on row 22.
_ZEUS_TOP = [
    "..........a.....a...........",  # 19
    "..........aa....aa..........",  # 20
    "..........acaaaaacaa........",  # 21
]
# Zeus's crown without the spikes behind the ear.
_CROWN = _ZEUS_TOP + [
    "........acccccacccbcdaa.....",  # 22
    ".......acbbccccacbbccdda....",  # 23
    "......acccbccabbbaccccdda...",  # 24
    "......abcbcbccbaabbccdccda..",  # 25
    "......abccbcbacccbbccbcdcda.",  # 26
    "......abcbcbbbaddccbccbcbcda",  # 27
    ".....acbbccbaaddcbbbcbcbbcda",  # 28
    ".....abcbccbcdddccbbaaabbada",  # 29
]
# Zeus's bangs over the forehead.
_BANGS = [
    ".....abcbccbcaaca.....abcaa.",  # 30
    ".....acbcbccbaaca....bbabaa.",  # 31
    ".....aacbbbca..aa...b..aba..",  # 32
]

FRONT = {
    # Zeus's own hair with the laurel filled in and the beard removed.
    'spiky': (19, [
        "..........a.....a...........",  # 19
        "..........aa....aa..........",  # 20
        "..........acaaaaacaa........",  # 21
        "......a...acccccacccaa......",  # 22
        "......aaaabbccccbabcdda.....",  # 23
        "......accccccccbbbacdda.....",  # 24
        ".......aabcbccbaabbccda.....",  # 25
        ".......abccbcbacccbbccbaa...",  # 26
        "......abcbcbbbaddccbccbcda..",  # 27
        "...aaacbbccbaaddcbbbcbcbbda.",  # 28
        "...abbcbccbcdddccbbaaabbada.",  # 29
    ] + _BANGS + _NAPE),
    # The same cut, combed: no spikes behind the ear.
    'short': (19, _CROWN + _BANGS + _NAPE),
    # Swept back from a widow's peak; the forehead stays open.
    'slick': (19, _ZEUS_TOP + [
        _row((8, "accbcccdcccddaa")),                       # 22
        _row((7, "acbbccacccdccccdda")),                    # 23
        _row((6, "acbbbcaaacccdccccdda")),                  # 24
        _row((6, "abbbcaccccaaaccdcccda")),                 # 25
        _row((6, "abbbacccdcccccaaacdcda")),                # 26
        _row((5, "abbbacccccdccccccaaacda")),               # 27
        _row((5, "abbaccccccccdccccccaaaa")),               # 28
        _row((5, "abbaccdcca"), (18, "accdca")),            # 29
        _row((5, "abbccca"), (22, "aa")),                   # 30
        _row((5, "abcbca")),                                # 31
        _row((5, "abbcca")),                                # 32
    ] + _NAPE),
    # Centre bangs from Zeus, then straight strands past the shoulders on both sides.
    'long': (19, _CROWN + [
        _row((4, "abcbccbcaaca"), (22, "abcca")),           # 30
        _row((4, "abcbcbccbaaca"), (21, "bbabca")),         # 31
        _row((4, "abcbcbbbca"), (16, "aa"), (20, "b"), (22, "abcca")),  # 32
        _row((3, "abcbcdcbca"), (23, "abca")),              # 33
        _row((3, "abcbcdcbca"), (23, "abcda")),             # 34
        _row((3, "abcabdcbca"), (23, "abcda")),             # 35
        _row((3, "abccbdcbca"), (23, "abcda")),             # 36
        _row((3, "abcbcdcaba"), (23, "abcda")),             # 37
        _row((3, "abcbcdccba"), (23, "abaca")),             # 38
        _row((3, "abcbcdcba"), (24, "abca")),               # 39
        _row((3, "abcbadcba"), (24, "abca")),               # 40
        _row((3, "abcbcdca"), (24, "abda")),                # 41
        _row((4, "abcdca"), (24, "aba")),                   # 42
        _row((4, "abaca"), (25, "a")),                      # 43
        _row((5, "a.a")),                                   # 44
    ]),
    # Shoulder-length waves that hide the ear.
    'wavy': (19, _CROWN + [
        _row((4, "abcbccbcaaca"), (22, "abcaa")),           # 30
        _row((4, "abbcbccbbaca"), (21, "bbabca")),          # 31
        _row((3, "abcbbbbcca"), (16, "aa"), (20, "b"), (23, "abca")),   # 32
        _row((2, "abccbdcbcba"), (23, "abca")),             # 33
        _row((2, "abcbcbdcbca"), (23, "abca")),             # 34
        _row((1, "abcbcbcdcbca"), (23, "aba")),             # 35
        _row((1, "abccbcbdcbca"), (24, "a")),               # 36
        _row((1, "abcbccbdcba")),                           # 37
        _row((2, "abcbcbdcba")),                            # 38
        _row((2, "abccbdcba")),                             # 39
        _row((3, "abcbdca")),                               # 40
        _row((3, "abcdba")),                                # 41
        _row((4, "aba")),                                   # 42
    ]),
    # Straight Egyptian bob with a cut fringe.
    'bob': (19, _ZEUS_TOP + [
        _row((8, "accbccbcdcdaa")),                         # 22
        _row((7, "acbccbccbcdcdda")),                       # 23
        _row((6, "acbcbccbccbcdcdda")),                     # 24
        _row((6, "abcbcbccbccbcdcdcda")),                   # 25
        _row((5, "abcbcbcbccbccbcdcdcda")),                 # 26
        _row((5, "abcbcbcbccbccbcdcbcdda")),                # 27
        _row((4, "abcbcbcbcbccbccbcdcbcdda")),              # 28
        _row((4, "abcbcbcbcbccbccbcdcbcdda")),              # 29
        _row((4, "abbbcbbbcbbbcbbbcbbbcbda")),              # 30
        _row((4, "abcbcbaabaabaabaabaabbca")),              # 31
        _row((4, "abcbcbcba"), (23, "abca")),               # 32
        _row((4, "abcbcdcba"), (23, "abca")),               # 33
        _row((4, "abcbcdcba"), (23, "abca")),               # 34
        _row((4, "abcbcdcba"), (23, "abda")),               # 35
        _row((4, "abcbcdcba"), (23, "abda")),               # 36
        _row((4, "abcbcdcba"), (23, "abca")),               # 37
        _row((4, "abbbbbbba"), (23, "aba")),                # 38
        _row((5, "aaaaaaa"), (24, "a")),                    # 39
    ]),
}
FRONT['ponytail'] = FRONT['short']

# Hair that hangs behind the shoulders, drawn before the torso.
BACK = {
    'long': (40, [
        ".abca...",  # 40
        "abbdca..",  # 41
        "abcdca..",  # 42
        "abbdca..",  # 43
        ".abdca..",  # 44
        ".abbca..",  # 45
        "..abca..",  # 46
        "..aba...",  # 47
        "...a....",  # 48
    ]),
    'ponytail': (21, [
        "......aaaa.",  # 21
        "....aacdcca",  # 22
        "...acdcbbca",  # 23
        "..accbbba..",  # 24
        ".acdcbba...",  # 25
        ".abdcba....",  # 26
        "abbdcba....",  # 27
        "abcdca.....",  # 28
        "abbdca.....",  # 29
        "abcda......",  # 30
        ".abda......",  # 31
        ".abca......",  # 32
        "..aba......",  # 33
        "...a.......",  # 34
    ]),
    'braid': (39, [
        "..acda....",  # 39
        "..abca....",  # 40
        "...aca....",  # 41
        "..acdca...",  # 42
        "..abbca...",  # 43
        "...aca....",  # 44
        "..acdca...",  # 45
        "..abbca...",  # 46
        "...aca....",  # 47
        "...a7a....",  # 48
        "....a.....",  # 49
    ]),
}
BACK_X = {'long': 15, 'ponytail': 12, 'braid': 15}
