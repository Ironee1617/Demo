-- Paints Poseidon in chuchuking's Spine part layout (same regions as Zeus).
-- aseprite -b --script-param zeus=<Spine/Zeus/Zeus.png> --script-param out=<this folder> --script paint_poseidon.lua
-- Body frames reuse the shared chibi face/torso of Zeus recolored for the sea;
-- hair, crown, cape foam and trident are drawn here.
local P = app.params
local OUT = P.out
local pc = app.pixelColor

local function hex(s)
  return pc.rgba(tonumber(s:sub(2, 3), 16), tonumber(s:sub(4, 5), 16), tonumber(s:sub(6, 7), 16), 255)
end

local function key(px)
  if pc.rgbaA(px) == 0 then return nil end
  return string.format("#%02x%02x%02x", pc.rgbaR(px), pc.rgbaG(px), pc.rgbaB(px))
end

-- Zeus palette -> Poseidon palette (skin, gold, eye lash and mouth stay).
-- Head: Zeus's white hair and beard become deep sea teal. Torso keeps a white chiton.
local REMAP = {
  ["#4c4c4c"] = "#0f2c3a", ["#f0f0f0"] = "#3d9ea3", ["#b3b3b3"] = "#25717f", ["#ffffff"] = "#8fd5cf",
  ["#2e4768"] = "#1c5c66", ["#5281b5"] = "#3fb3a8",
  ["#3765be"] = "#1f6fae", ["#274894"] = "#184f86",
  ["#282628"] = "#0f2230", ["#363036"] = "#173246", ["#494348"] = "#23475c",
}
local TORSO_REMAP = { ["#f0f0f0"] = "#d5efeb", ["#b3b3b3"] = "#8fbfc0", ["#ffffff"] = "#f3fcfa" }
local ZEUS_CAPE = "#102963"
local HAIR = { O = "#0f2c3a", ["3"] = "#8fd5cf", ["1"] = "#3d9ea3", ["2"] = "#25717f", ["4"] = "#1c5160" }
local GOLD = { G = "#7a500a", ["5"] = "#f5cb85", ["6"] = "#c28f3b", Y = "#fff4d2", t = "#5fe3d6", T = "#1a8f8f",
  s = "#10303d", S = "#2a7286", L = "#4aa3b0" }
local CAPE = { base = "#0f4a6b", crest = "#2f86a6", under = "#0a3350" }

-- Zeus atlas (Spine export): body frames 0-3, arm0, leg0.
local zeus = app.open(P.zeus)
local zcel = zeus.cels[1]
local function zpx(x, y)
  local ix, iy = x - zcel.position.x, y - zcel.position.y
  if ix < 0 or iy < 0 or ix >= zcel.image.width or iy >= zcel.image.height then return 0 end
  return zcel.image:getPixel(ix, iy)
end
local FRAME_X, FRAME_Y = { 2, 65, 128, 191 }, 51

local function headDy(k)
  for y = 0, 74 do
    for x = 0, 60 do
      if key(zpx(FRAME_X[k] + x, FRAME_Y + y)) == "#0b0e1c" then return y - 33 end
    end
  end
  error("no eye lash in Zeus frame " .. k)
end

-- Face, beard and torso of the shared chibi base; Zeus's spiky crown hair and laurel are dropped,
-- and below the ear the back hair replaces the left edge of his beard so the nape stays full.
local function keepBase(x, y, dy)
  return (y >= 26 + dy and x >= 26) or (y >= 33 + dy and y < 36 + dy and x >= 22) or (y >= 36 + dy and x >= 23)
    or (y >= 41 and x >= 24)
end

-- Hair masks as {y, x0, x1} spans; lines are lock boundaries; silhouette outline and shading are automatic.
-- The back of the head stays close to the skull; the hair falls down the back in S-shaped waves.
-- Head width matches Zeus and Odin (left edge x18, round crown corner). The right side tucks under
-- the crown and meets the side strands of the shared base; the back is shoulder-length wavy hair.
local HAIR_BACK = {
  { 19, 25, 34 }, { 20, 24, 37 }, { 21, 22, 38 }, { 22, 21, 39 }, { 23, 20, 39 },
  { 24, 19, 40 }, { 25, 19, 40 }, { 26, 18, 40 }, { 27, 18, 40 }, { 28, 18, 40 },
  { 29, 18, 40 }, { 30, 18, 40 }, { 31, 18, 40 }, { 32, 18, 39 },
  { 33, 18, 21 }, { 34, 17, 21 }, { 35, 17, 21 }, { 36, 17, 22 }, { 37, 16, 22 }, { 38, 16, 22 },
  { 39, 16, 22 }, { 40, 16, 23 }, { 41, 16, 23 }, { 42, 16, 23 }, { 43, 15, 23 }, { 44, 15, 22 },
  { 45, 15, 17 }, { 45, 19, 22 }, { 46, 16, 17 }, { 46, 20, 21 }, { 47, 21, 21 },
}
-- Lock lines sweep from the crown down to the nape.
local HAIR_BACK_LINES = {
  { 25, 25 }, { 24, 26 }, { 23, 26 }, { 22, 27 }, { 21, 27 }, { 20, 28 }, { 19, 28 },
  { 23, 31 }, { 22, 32 }, { 21, 33 }, { 20, 34 }, { 19, 34 }, { 18, 35 },
}
-- Crown shadow, then the back hair painted by hand: one mass that parts only near the curled ends.
local HAIR_BACK_PAINT = {
  { 24, 27, "22222222222" },
  { 36, 18, "3112" }, { 37, 17, "13112" }, { 38, 17, "31122" }, { 39, 17, "11O22" },
  { 40, 17, "21O112" }, { 41, 17, "21O312" }, { 42, 17, "1O3112" }, { 43, 16, "21O1122" },
  { 44, 16, "22O124" }, { 45, 16, "4" }, { 45, 20, "24" },
}
local CROWN = {
  { 16, 32, "G" }, { 17, 31, "GYG" },
  { 18, 28, "G" }, { 18, 31, "G5G" }, { 18, 36, "G" },
  { 19, 27, "GYG" }, { 19, 31, "G5G" }, { 19, 35, "GYG" },
  { 20, 27, "G5GG565GG5G" },
  { 21, 26, "G55555t55555G" },
  { 22, 26, "G66666T66666G" },
  { 23, 27, "GGGGGGGGGGG" },
}

local function shadeHair(spans, lines, paint, dy)
  local m = {}
  local function get(x, y) return m[y] and m[y][x] end
  local function set(t, x, y, c) t[y] = t[y] or {}; t[y][x] = c end
  for _, s in ipairs(spans) do
    for x = s[2], s[3] do set(m, x, s[1] + dy, "#") end
  end
  for _, p in ipairs(lines or {}) do
    if get(p[1], p[2] + dy) then set(m, p[1], p[2] + dy, "O") end
  end
  local edge = {}
  for y, row in pairs(m) do
    for x, c in pairs(row) do
      if c == "#" and not (get(x - 1, y) and get(x + 1, y) and get(x, y - 1) and get(x, y + 1)) then
        edge[#edge + 1] = { x, y }
      end
    end
  end
  for _, p in ipairs(edge) do set(m, p[1], p[2], "O") end
  local out = {}
  for y, row in pairs(m) do
    for x, c in pairs(row) do
      if c == "#" then
        if get(x, y - 1) == "O" then c = "3"
        elseif get(x, y + 1) == "O" then c = x < 19 and "4" or "2"
        else c = "1" end
      end
      set(out, x, y, c)
    end
  end
  for _, s in ipairs(paint or {}) do
    for i = 1, #s[3] do
      local c = s[3]:sub(i, i)
      if c ~= "." and get(s[2] + i - 1, s[1] + dy) then set(out, s[2] + i - 1, s[1] + dy, c) end
    end
  end
  return out
end

local function blit(img, cells, pal)
  for y, row in pairs(cells) do
    for x, c in pairs(row) do
      if pal[c] and x >= 0 and y >= 0 and x < img.width and y < img.height then img:drawPixel(x, y, hex(pal[c])) end
    end
  end
end

local function blitRows(img, rows, pal, dy)
  for _, s in ipairs(rows) do
    for i = 1, #s[3] do
      local c = s[3]:sub(i, i)
      if pal[c] then img:drawPixel(s[2] + i - 1, s[1] + dy, hex(pal[c])) end
    end
  end
end

local function isCape(x, y, k)
  if x < 0 or y < 0 or x > 60 or y > 74 then return false end
  return key(zpx(FRAME_X[k] + x, FRAME_Y + y)) == ZEUS_CAPE
end

-- Bigger cape: Zeus's cape motion stretched 1.6x to the left and 1.4x down from the shoulder
-- (top moves up to row 40 so the hem stays at or above the feet, row 54), then smoothed.
local function capeMask(k)
  local m, out = {}, {}
  local function at(t, x, y) return t[y * 64 + x] end
  for y = 36, 58 do
    for x = 0, 25 do
      local sx = math.floor(25 - (25 - x) / 1.6 + 0.5)
      local sy = math.floor(43 + (y - 40) / 1.4 + 0.5)
      if isCape(sx, sy, k) then m[y * 64 + x] = true end
    end
  end
  for _ = 1, 2 do
    for y = 36, 58 do
      for x = 0, 25 do
        local n = 0
        for oy = -1, 1 do
          for ox = -1, 1 do
            if (ox ~= 0 or oy ~= 0) and at(m, x + ox, y + oy) then n = n + 1 end
          end
        end
        local v = at(m, x, y)
        if v and n <= 2 then v = false elseif not v and n >= 5 then v = true end
        if v then out[y * 64 + x] = true end
      end
    end
    m, out = out, {}
  end
  return m, at
end

local function paintFrame(k)
  local dy = headDy(k)
  local cape, body = Image(61, 75), Image(61, 75)
  local cm, at = capeMask(k)
  for y = 36, 58 do
    for x = 0, 25 do
      if at(cm, x, y) then
        local col = CAPE.base
        if not at(cm, x, y - 1) then col = CAPE.crest elseif not at(cm, x, y + 1) then col = CAPE.under end
        cape:drawPixel(x, y, hex(col))
      end
    end
  end
  for y = 0, 74 do
    for x = 0, 60 do
      local c = key(zpx(FRAME_X[k] + x, FRAME_Y + y))
      if c == ZEUS_CAPE then
        -- replaced by the bigger cape above
      elseif c and keepBase(x, y, dy) then
        local torso = y >= 42 + (dy > 0 and 1 or 0)
        body:drawPixel(x, y, hex((torso and TORSO_REMAP[c]) or REMAP[c] or c))
      end
    end
  end
  local back, crown = Image(61, 75), Image(61, 75)
  blit(back, shadeHair(HAIR_BACK, HAIR_BACK_LINES, HAIR_BACK_PAINT, dy), HAIR)
  blitRows(crown, CROWN, GOLD, dy)
  return { cape = cape, hair_back = back, body = body, crown = crown }
end

-- Body sprite: 4 idle frames, layered for hand edits.
local spr = Sprite(61, 75, ColorMode.RGB)
local ORDER = { "cape", "hair_back", "body", "crown" }
local layers = { cape = spr.layers[1] }
layers.cape.name = "cape"
for i = 2, #ORDER do
  layers[ORDER[i]] = spr:newLayer()
  layers[ORDER[i]].name = ORDER[i]
end
for k = 2, 4 do spr:newEmptyFrame(k) end
for k = 1, 4 do
  spr.frames[k].duration = 0.15
  local f = paintFrame(k)
  for _, name in ipairs(ORDER) do spr:newCel(layers[name], k, f[name], Point(0, 0)) end
end
spr:newTag(1, 4).name = "idle"
spr:saveAs(OUT .. "/Poseidon.aseprite")

local frames = {}
for k = 1, 4 do
  local img = Image(61, 75)
  img:drawSprite(spr, k)
  img:saveAs(OUT .. "/Export/Poseidon_" .. (k - 1) .. ".png")
  frames[k] = img
end

-- Arm and sandal: Zeus parts recolored; the weapon arm is the mirrored arm like Weapons/Zeus_arm.png.
local function part(rx, ry, w, h, mirror)
  local img = Image(w, h)
  for y = 0, h - 1 do
    for x = 0, w - 1 do
      local c = key(zpx(rx + x, ry + y))
      if c then img:drawPixel(mirror and (w - 1 - x) or x, y, hex(REMAP[c] or c)) end
    end
  end
  return img
end

local function savePart(img, name)
  local s = Sprite(img.width, img.height, ColorMode.RGB)
  s:newCel(s.layers[1], 1, img, Point(0, 0))
  s.layers[1].name = name
  s:saveAs(OUT .. "/" .. name .. ".aseprite")
  img:saveAs(OUT .. "/Export/" .. name .. ".png")
  s:close()
end

local arm0, leg0 = part(2, 24, 20, 25, false), part(2, 2, 20, 20, false)
savePart(arm0, "Poseidon_arm0")
savePart(leg0, "Poseidon_leg0")
savePart(part(2, 24, 20, 25, true), "Poseidon_arm")

-- Trident: vertical on the 61x75 weapon canvas like Weapons/Odin_weapon.png.
local TRIDENT = {
  "......G......", ".....GYG.....", ".....G5G.....", "..G.G5Y6G.G..", ".GYGGG5GGGYG.",
  "G55G.G5G.G55G", "GG5G.G6G.G5GG", ".G6G.G5G.G6G.", ".G6G.G6G.G6G.", ".G6G.G5G.G6G.",
  ".G6G.G6G.G6G.", ".G6GGG6GGG6G.", ".G5656t6565G.", "..G666T666G..", "...GGG6GGG...",
  ".....G5G.....", ".....G6G.....",
}
local weapon = Image(61, 75)
local wrows = {}
for i, r in ipairs(TRIDENT) do wrows[#wrows + 1] = { 4 + i, 24, r } end
for y = 22, 59 do
  local r = ".....sSs....."
  if y == 22 or y == 59 then r = ".....GGG....."
  elseif y == 34 or y == 50 then r = ".....G5G....."
  elseif y >= 40 and y <= 44 and y % 2 == 0 then r = ".....sLs....." end
  wrows[#wrows + 1] = { y, 24, r }
end
for i, r in ipairs({ "....G565G....", "....G6Y6G....", ".....G5G.....", "......G......" }) do
  wrows[#wrows + 1] = { 59 + i, 24, r }
end
blitRows(weapon, wrows, GOLD, 0)
savePart(weapon, "Poseidon_weapon")

-- Spine-ready atlas page with Zeus's region layout, so Zeus.skel can drive it.
local page = Image(256, 128)
for k = 1, 4 do page:drawImage(frames[k], Point(FRAME_X[k], FRAME_Y)) end
page:drawImage(arm0, Point(2, 24))
page:drawImage(leg0, Point(2, 2))
page:saveAs(OUT .. "/Spine/Poseidon.png")

print("painted Poseidon: head dy " .. headDy(1) .. "," .. headDy(2) .. "," .. headDy(3) .. "," .. headDy(4))
spr:close()
zeus:close()
