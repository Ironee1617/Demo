"""Weapons and off-hand items, drawn pointing up. Each entry: (rows, (grip_x, grip_y)).

The fist is drawn over the grip pixel at run time, so a weapon rotates around the hand.
Legend (pixkit.GEAR): blade m n o p P, gold G g h H y, wood 0..3, ribbon 5..8,
gem z j J, glow ~ * + %, dark q k K D, leaf f F e, string s, white w W, ink #.
"""


def sword(blade=20, guard="GhhyhhG", grip=3, pommel="hHh", tip=("..P..", ".pPo."), body=".pPo.", flame=False):
    rows = list(tip)
    for i in range(blade):
        rows.append(body if not flame or i % 3 else ".pP*.")
    rows.append(guard)
    for _ in range(grip):
        rows.append("..6..".center(len(guard), '.'))
    rows.append(pommel.center(len(guard), '.'))
    width = max(len(r) for r in rows)
    rows = [r.center(width, '.') for r in rows]
    return rows, (width // 2, len(rows) - 1 - grip // 2 - 1)


def pole(head, shaft=30, ch='2', butt=("..h..", "..G..")):
    width = max(len(r) for r in head)
    rows = [r.center(width, '.') for r in head]
    for _ in range(shaft):
        rows.append(ch.center(width, '.'))
    rows += [b.center(width, '.') for b in butt]
    return rows, (width // 2, len(head) + int(shaft * .62))


def bow(height=14, wood='2', light='3', string='s', tip='h'):
    """A recurve bow facing right: limbs on the right, string on the left."""
    rows = []
    half = height // 2
    curve = [3, 4, 4, 5, 5, 5, 5, 4, 4, 3]
    for i in range(height):
        d = min(i, height - 1 - i)
        off = curve[min(d, len(curve) - 1)] if d < len(curve) else 5
        line = [ '.' ] * 8
        line[0] = string
        if i in (0, height - 1):
            line[0] = tip
            line[1] = tip
        else:
            line[min(7, off)] = wood if i % 4 else light
            if off > 1:
                line[min(7, off) - 1] = wood if i != half else 'H'
        rows.append(''.join(line))
    rows[half] = rows[half][:3] + "hHh" + rows[half][6:]
    return rows, (4, half)


W = {}

W['holy_sword'] = sword(22, guard="GhhyhhG", pommel="hJh")
W['fire_sword'] = sword(20, guard="GhhhG", pommel="hjh", tip=("..*..", ".+%+."), body=".p%*.", flame=True)
W['iron_sword'] = sword(18, guard="ooPoo", pommel=".o.")
W['bronze_sword'] = (["...H...", "..hHh..", ".hhHhh.", ".hhHhh.", "hhhHhhg", "hhhHhhg", ".hhHhg.", "..hHg..",
                      "..hHg..", ".hhHhg.", "hhhHhhg", "hhhHhhg", ".hhHhg.", "..hHg..", "..hHg..", "GhhyhhG",
                      "...6...", "...6...", "...6...", "..hHh.."], (3, 17))
W['dark_sword'] = sword(20, guard="qKKDKKq", pommel="KjK", tip=("..D..", ".kDK."), body=".kDK.")
W['dagger'] = sword(9, guard="GhhhG", grip=2, pommel=".h.", tip=("..P..", ".pPo."))
W['green_dagger'] = sword(9, guard="GhhhG", grip=2, pommel=".j.", tip=("..+..", ".pP*."), body=".pP*.")
W['khopesh'] = (["......opP", ".....opP.", "....opP..", "...opP...", "...oP....", "..opP....", "..oP.....",
                 "..oP.....", "..oP.....", "..opP....", "..hHh....", "...6.....", "...6.....", "...6.....",
                 "..hHh...."], (3, 12))
W['saber'] = (["....P", "...pP", "...pP", "..opP", "..opP", ".opP.", ".opP.", ".opP.", "opP..", "opP..",
               "opP..", "opP..", "GhhyG", ".66..", ".66..", ".66..", ".hH.."], (1, 14))

W['trident'] = pole(["h...h...h", "H...H...H", "hh..h..hh", ".hh.h.hh.", "..hhhhh..", "...hHh...", "....h...."], 30, '2')
W['gold_trident'] = pole(["H...H...H", "h...h...h", "hh..h..hh", ".hhhhhhh.", "...hHh...", "....h...."], 30, 'h')
W['trishula'] = pole(["...P...", "p..P..p", "p.pPp.p", "pp.P.pp", ".ppPpp.", "..ooo..", "...G...", "..6.6..", "...6..."], 30, '2')
W['bident'] = pole(["*....*", "D....D", "D....D", "DK..KD", ".DKKD.", "..DD..", "..qq.."], 30, 'K', ("..K..", "..q.."))
W['spear'] = pole(["..P..", ".pPo.", ".pPo.", "opPoo", ".pPo.", "..o..", ".GhG."], 34, '2')
W['gold_spear'] = pole(["..y..", ".hyH.", ".hyH.", "hhyHh", ".hyh.", "..h..", ".GhG."], 34, '2')
W['gungnir'] = pole(["..P..", ".pPo.", ".pPo.", ".pPo.", "opPoo", ".pPo.", "..o..", ".jhj.", ".6.6.", "..6.."], 34, '1')
W['gae_bolg'] = pole(["...P...", "..pPo..", ".opPoo.", "oo.P.oo", "o.pPo.o", "..pPo..", "o.oPo.o",
                      ".oohoo.", "..GhG.."], 34, '6', ("..6..", "..5.."))
W['fire_spear'] = pole(["..+..", ".+%+.", "*+%+*", ".pPo.", ".pPo.", "..o..", ".GhG.", ".5.5.", "..5.."], 32, '6', ("..6..", "..h.."))
W['sun_spear'] = pole([".y.y.y.", "..yHy..", "y.hHh.y", ".hhHhh.", "..hHh..", "..hHh..", "...h...", "..GhG.."], 34, 'h')
W['sanjian'] = pole([".P.P.P.", ".PpPpP.", ".pPPPo.", "..pPo..", "..pPo..", "..pPo..", "...o...", "..GhG.."], 32, '2')
W['guandao'] = (["....pP.", "...pPP.", "..opPP.", "..opPP.", ".oopPP.", ".oopPP.", "oooppP.", "ooopPP.",
                 ".oooPP.", "..ooPP.", "....oP.", "..GhhG.", "...2...", ] + ["...2..."] * 30 + ["..hHh..", "...G..."],
                (3, 34))
W['scythe'] = (["PPPPP......", "opppppP....", ".oooppppP..", "....oooopP.", ".......ooP.", "........Ko.",
                "........K..", ] + ["........K.."] * 30 + [".......qKq."], (8, 26))

W['hammer'] = (["noooooooooon", "oPPPPPPPPPpo", "oPppppppppno", "oppppppppppo", "opppppppppno", "noooooooooon",
                ".....hH.....", ".....56.....", ".....65.....", ".....56.....", ".....65.....", ".....56.....",
                ".....65.....", "....hHHh...."], (5, 10))
W['mace'] = (["..hhh..", ".hHHHh.", "hHyHyHh", "hHHHHHh", "hhHHHhh", ".hhhhh.", "..ghg..", "...h...",
              "...h...", "...h...", "...h...", "...h...", "...h...", "...h...", "...h...", "...h...",
              "..hHh.."], (3, 13))
W['club'] = (["..222..", ".23332.", "2233322", "2333332", "2233322", ".22322.", ".22222.", "..222..",
              "..222..", "...2...", "...2...", "...2...", "...2...", "...2...", "...1...", "...1...",
              "..1.1.."], (3, 13))
W['axe'] = (["...ppp...", ".opppPP..", "oooppP2..", "ooopp22..", ".oopp2...", "..o..2...", ".....2...",
             ".....2...", ".....2...", ".....2...", ".....2...", ".....2...", ".....2...", ".....2...",
             "....hHh.."], (5, 11))
W['great_axe'] = (["....2......", ".opp2ppo...", "oPPP2PPPo..", "oPpp2ppPo..", "oPpp2ppPo..", ".opp2ppo...",
                   "....2......", "....2......", "....2......", "....2......", "....2......", "....2......",
                   "....2......", "....2......", "....2......", "....2......", "....2......", "...hHh....."],
                  (4, 14))
W['ruyi_staff'] = (["hHh"] + ["hHh"] * 2 + ["777"] * 2 + [".7."] * 29 + ["777"] * 2 + ["hHh"] * 3, (1, 26))

W['staff_green'] = pole([".jjj.", "jJJjj", "jJjjj", ".jjj.", ".GhG.", "hh.hh", "h...h", ".hhh."], 30, '2')
W['flame_staff'] = pole(["..+..", ".+%+.", "*+%%*", "*+%+*", ".*+*.", ".GhG.", "hh.hh"], 30, '1')
W['ankh'] = pole([".hhh.", "h...h", "h...h", ".h.h.", "hhhhh", "..h..", "..j.."], 28, 'h', ("..h..", ".hHh."))
W['was_sun'] = pole([".*+*.", "*+%+*", "*+%+*", ".*+*.", "..o..", "hh...", ".hh..", "..h.."], 30, 'h', ("..h..", "h.h.."))
W['crook'] = (["..hHh.", ".h...H", "h....H", "h....h", ".....7", ".....h", ".....7", ".....h"] +
              [".....7", ".....h"] * 11 + ["....hHh"], (5, 19))
W['flail'] = (["5.5.5", "7.7.7", "7.7.7", "7.7.7", "h.h.h", ".hhh.", "..h..", "..7..", "..h..", "..7..",
               "..h..", ".hHh."], (2, 9))
W['tome'] = (["wwwwww.wwwwww", "wWWWWw0wWWWWw", "wW##Ww0wW##Ww", "wWWWWw0wWWWWw", "wW##Ww0wW##Ww",
              "wWWWWw0wWWWWw", "000000000000.", ".11111111111."], (6, 6))
W['lotus'] = pole(["..7.7..", ".787.7.", "7887887", "7888887", ".78887.", "..fff..", "...F..."], 26, 'F', ("..F..", "..f.."))
W['orb_staff'] = pole([".jjj.", "jJJjj", "jJjjj", "jjjjz", ".zzz.", "hhGhh", ".hHh."], 28, '2')
W['rainbow_stone'] = (["..zzz..", ".zjjJz.", "zjjJJjz", "zjjjjjz", ".zjjjz.", "..zzz.."], (3, 5))
W['orb'] = (["..zzz..", ".zjjJz.", "zjjJJjz", "zjjjjjz", "zzjjjzz", ".zzzzz.", "..hhh..", ".GhhhG."], (3, 7))
W['pearl'] = (["..www..", ".wwWWw.", "wwwWWww", "wwwwwww", "wwwwwww", ".wwwww.", "..www.."], (3, 5))
W['chakra'] = (["..h.h.h..", ".hhhhhhh.", "hhy...yhh", ".hy...yh.", "hhh...hhh", ".hy...yh.", "hhy...yhh",
                ".hhhhhhh.", "..h.h.h.."], (4, 4))
W['bell_staff'] = pole(["..h..", ".hHh.", "hhHhh", "hhhhh", "GhhhG", ".jJj.", "..j..", "h.h.h", "h.h.h"], 28, '2')
W['flower_staff'] = pole(["..7..", ".787.", "78887", ".787.", "..7..", ".fFf.", "f.F.f"], 28, 'F', ("..F..", "..f.."))
W['mirror_staff'] = pole([".hhhhh.", "hhpPphh", "hpPPpPh", "hpPppph", "hhppphh", ".hhhhh.", "..GhG.."], 28, '2')
W['scepter'] = pole([".KjK.", "KjJjK", "KjjjK", ".KjK.", "..D..", ".qDq."], 26, 'D', ("..D..", ".qKq."))
W['hawk_staff'] = pole(["..00...", ".0110..", "0w1100.", ".0hh11.", "..0111.", "...011.", "...0..."], 28, '1')
W['horn'] = (["hhh......", "hHyh.....", "hhhhh....", ".hhhhh...", "..hhhhh..", "...hhhhh.", "....hhhh.",
              ".....hhh.", ".....hhh.", "....GhG..", ".....h...", ".....h..."], (5, 10))
W['harp'] = (["..hHhh..", ".h....H.", "h.s.s..h", "h.s.s.s.h", "h.s.s.s.h", "h.s.s.s.h", ".hs.s.s.h",
              "..hhhhhh.", "...hh....", "...h....."], (3, 8))
W['pestle'] = ([".hhhhh.", "hHHHHHh", "hHyyyHh", "hhhhhhh", ".hhhhh.", "...2...", "...2...", "...2...",
                "...2...", "...2...", "...2...", "...2...", "...1..."], (3, 9))
W['bolt'] = None  # chuchuking's Zeus/Weapon.png

# Bows
W['gold_bow'] = bow(22)
W['silver_bow'] = bow(22)
W['wood_bow'] = bow(22)
W['big_bow'] = bow(26)
W['flower_bow'] = bow(22)

# Off-hand items
W['shield_round'] = (["...ooooo...", ".oopppppoo.", "oopphhhppoo", "opphhyhhppo", "ophhyyyhhpo", "ophyyjyyhpo",
                      "ophhyyyhhpo", "opphhyhhppo", "oopphhhppoo", ".oopppppoo.", "...ooooo..."], (5, 5))
W['shield_aegis'] = (["...hhhhh...", ".hhoooooHh.", "hhoopppooHh", "hoopkKkpooh", "hoopKjKpooh", "hoopkKkpooh",
                      "hhoopppooHh", ".hhoooooHh.", "...hhhhh..."], (5, 4))
W['buckler'] = (["..ooooo..", ".oppPPPo.", "oppHyHPpo", "opHyyyHpo", "oppHyHppo", ".ooppppo.", "..ooooo.."], (4, 3))
W['ring'] = (["..hhhh..", ".hHHHHh.", "hH....Hh", "hH....hh", "hh....hh", "hh....hh", ".hhhhhh.", "..hhhh.."], (1, 4))
W['bronze_mirror'] = (["..hhhhh..", ".hHHHHHh.", "hHjHyHjHh", "hHHHHHHHh", "hHjHyHjHh", ".hHHHHHh.", "..hhhhh.."], (4, 3))
W['conch'] = (["..ww..", ".wWWw.", "wWwwww", "wwwWw.", ".www..", "..w..."], (2, 3))
