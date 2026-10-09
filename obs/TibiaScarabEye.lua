-- Tibia Scarab Eye: overlays as native OBS scene items.
-- The desktop app publishes geometry only (obs-layout.json). This script reads the
-- REAL on-canvas transform of the game-capture item (draw transform + crop) and places
-- each overlay in canvas coordinates computed from it, so no transform ever has to be
-- copied by hand and nothing is hidden inside a custom-draw source.
local obs = obslua

local PREFIX = "Tibia Scarab Eye "
local PALETTE = {light = 0xff7e8585, middle = 0xff4a4f4f, dark = 0xff151616, black = 0xff000000, guide = 0xffffff00}
local cfg = {source = "", path = "", dx = 0, dy = 0, guides = false, manual = true}
local overrides, overrides_dirty, overrides_saved = {}, false, 0 -- "scene/region id" -> {x, y, sx, sy}
local add_crop_item, add_opacity -- defined below
local colors = {}
local built = {}        -- scene name -> {sig = string, items = {scene items}}
local scene_names = {}
local scene_refresh = 0
local packet, packet_time = nil, 0
local last_log = ""
local warned_nested = {} -- scene name -> true once the "capture inside a group" warning was logged

local function log(msg) obs.script_log(obs.LOG_INFO, "Tibia Scarab Eye: " .. msg) end
local function default_path() return (os.getenv("LOCALAPPDATA") or "") .. "\\TibiaScarabEye\\obs-layout.json" end
local function round(v) return math.floor(v + 0.5) end
local function clamp(v, lo, hi) return math.max(lo, math.min(hi, v)) end
local function num(d, k) return obs.obs_data_get_double(d, k) end

-- ---------------------------------------------------------------- packet
local function read_packet(path)
    local file = io.open(path, "rb")
    if not file then return nil end
    local raw = file:read(1048577)
    file:close()
    if not raw or #raw > 1048576 then return nil end
    local data = obs.obs_data_create_from_json(raw)
    if not data then return nil end
    local p
    local layout = obs.obs_data_get_obj(data, "Layout")
    if layout then
        p = {
            version = obs.obs_data_get_int(data, "Version"),
            heartbeat = obs.obs_data_get_int(data, "Heartbeat"),
            key = obs.obs_data_get_string(data, "Session") .. ":" .. obs.obs_data_get_int(data, "Revision"),
            enabled = obs.obs_data_get_bool(layout, "Enabled"),
            cw = num(layout, "Width"), ch = num(layout, "Height"), regions = {},
        }
        local array = obs.obs_data_get_array(layout, "Regions")
        if array then
            for i = 0, math.min(30, obs.obs_data_array_count(array)) - 1 do
                local r = obs.obs_data_array_item(array, i)
                if r then
                    p.regions[#p.regions + 1] = {
                        id = obs.obs_data_get_string(r, "Id"), visible = obs.obs_data_get_bool(r, "Visible"),
                        left = num(r, "Left"), top = num(r, "Top"), w = num(r, "Width"), h = num(r, "Height"),
                        opacity = num(r, "Opacity"),
                        cropx = num(r, "CropX"), cropy = num(r, "CropY"), cropw = num(r, "CropW"), croph = num(r, "CropH"),
                        cx = num(r, "ContentX"), cy = num(r, "ContentY"), cw = num(r, "ContentW"), ch = num(r, "ContentH"),
                    }
                    obs.obs_data_release(r)
                end
            end
            obs.obs_data_array_release(array)
        end
        obs.obs_data_release(layout)
    end
    obs.obs_data_release(data)
    return p
end

-- ---------------------------------------------------------------- scene helpers
local function place(item, x, y, sx, sy)
    obs.obs_sceneitem_set_alignment(item, 5) -- OBS_ALIGN_LEFT (1) | OBS_ALIGN_TOP (4)
    local p, s = obs.vec2(), obs.vec2()
    p.x, p.y, s.x, s.y = x, y, sx, sy
    obs.obs_sceneitem_set_pos(item, p)
    obs.obs_sceneitem_set_scale(item, s)
end

local function rect(scene, color, x, y, w, h)
    if w <= 0 or h <= 0 then return end
    place(obs.obs_scene_add(scene, colors[color]), x, y, w, h)
end

-- Items are kept alive with our own reference, so a group deleted by the user (or by undo/redo)
-- never leaves a dangling pointer behind.
local function add_group(scene, name)
    local group = obs.obs_scene_add_group(scene, name)
    if group then obs.obs_sceneitem_addref(group) end
    return group
end

local function alive(record)
    for _, item in ipairs(record.items) do
        if not obs.obs_sceneitem_get_scene(item) then return false end
    end
    return true
end

local function remove_items(record)
    if not record then return end
    for _, item in ipairs(record.items) do
        obs.obs_sceneitem_remove(item) -- no-op when already removed
        obs.obs_sceneitem_release(item)
    end
    record.items = {}
    record.groups = {}
end

-- Groups holding private sources must never reach the saved scene collection.
local function purge(scene)
    local items = obs.obs_scene_enum_items(scene)
    if not items then return end
    for _, item in ipairs(items) do
        if obs.obs_sceneitem_is_group(item) and obs.obs_source_get_name(obs.obs_sceneitem_get_source(item)):sub(1, #PREFIX) == PREFIX then
            obs.obs_sceneitem_remove(item)
        end
    end
    obs.sceneitem_list_release(items)
end

local function for_each_scene(fn)
    local scenes = obs.obs_frontend_get_scenes()
    if not scenes then return end
    for _, source in ipairs(scenes) do fn(obs.obs_source_get_name(source), obs.obs_scene_from_source(source)) end
    obs.source_list_release(scenes)
end

local clear_all
local poll_user_ref
function clear_all()
    for name, record in pairs(built) do
        if cfg.manual and poll_user_ref then poll_user_ref(record) end -- keep the last user move
        remove_items(record)
    end
    built = {}
end

-- ---------------------------------------------------------------- manual positions
local function store_path()
    local dir = cfg.path:match("^(.*)[/\\][^/\\]*$") or "."
    return dir .. "\\obs-manual.txt"
end

local function load_overrides()
    overrides = {}
    local f = io.open(store_path(), "r")
    if not f then return end
    for line in f:lines() do
        local key, x, y, sx, sy = line:match("^([^\t]+)\t([^\t]+)\t([^\t]+)\t([^\t]+)\t([^\t]+)$")
        if key and tonumber(x) and tonumber(y) and tonumber(sx) and tonumber(sy) then
            overrides[key] = {x = tonumber(x), y = tonumber(y), sx = tonumber(sx), sy = tonumber(sy)}
        end
    end
    f:close()
end

local function save_overrides()
    local f = io.open(store_path(), "w")
    if not f then return end
    for key, o in pairs(overrides) do f:write(string.format("%s\t%.4f\t%.4f\t%.6f\t%.6f\n", key, o.x, o.y, o.sx, o.sy)) end
    f:close()
    overrides_dirty = false
end

-- ---------------------------------------------------------------- build
-- Content: the same game source, cropped, mapped from capture pixels to a destination rect
-- (x, y, w, h in the coordinate space of the parent scene).
function add_crop_item(inner, base, r, sw, sh, x, y, w, h)
    local left = clamp(round(r.cropx * sw), 0, sw - 1)
    local top = clamp(round(r.cropy * sh), 0, sh - 1)
    local right = clamp(round((r.cropx + r.cropw) * sw), left + 1, sw)
    local bottom = clamp(round((r.cropy + r.croph) * sh), top + 1, sh)
    local content = obs.obs_scene_add(inner, base)
    local crop = obs.obs_sceneitem_crop()
    crop.left, crop.top, crop.right, crop.bottom = left, top, sw - right, sh - bottom
    obs.obs_sceneitem_set_crop(content, crop)
    place(content, x, y, w / (right - left), h / (bottom - top))
end

function add_opacity(group, value)
    local opacity = clamp(round(value), 20, 100)
    if opacity >= 100 then return end
    local s = obs.obs_data_create()
    obs.obs_data_set_int(s, "opacity", opacity)
    local filter = obs.obs_source_create_private("color_filter", PREFIX .. "opacidade", s)
    obs.obs_data_release(s)
    if filter then
        obs.obs_source_filter_add(obs.obs_sceneitem_get_source(group), filter)
        obs.obs_source_release(filter)
    end
end

-- T maps client pixels (the app's coordinate system) to canvas pixels.
local function add_region(scene, base, r, T)
    if not r.visible or r.w < 6 or r.h < 6 or r.w > 10006 or r.h > 10006 then return nil end
    if r.cw <= 0 or r.ch <= 0 then return nil end
    local group = add_group(scene, PREFIX .. r.id:sub(1, 6))
    if not group then return nil end
    obs.obs_sceneitem_defer_group_resize_begin(group)
    local inner = obs.obs_sceneitem_group_get_scene(group)

    -- Frame (client-space rects, clipped to the game area exactly like the app clips the crop).
    local function frame(color, ox, oy, w, h)
        local x0, y0 = math.max(0, r.left + ox), math.max(0, r.top + oy)
        local x1, y1 = math.min(T.cw, r.left + ox + w), math.min(T.ch, r.top + oy + h)
        if x1 > x0 and y1 > y0 then rect(inner, color, T.ox + x0 * T.sx, T.oy + y0 * T.sy, (x1 - x0) * T.sx, (y1 - y0) * T.sy) end
    end
    frame("dark", 0, 0, r.w, r.h)
    frame("light", 0, 0, r.w, 1)
    frame("light", 0, 0, 1, r.h)
    frame("middle", 1, 1, r.w - 2, r.h - 2)
    frame("dark", 2, 2, r.w - 4, r.h - 4)
    frame("black", 3, 3, r.w - 6, r.h - 6)

    add_crop_item(inner, base, r, T.sw, T.sh, T.ox + (r.left + r.cx) * T.sx, T.oy + (r.top + r.cy) * T.sy, r.cw * T.sx, r.ch * T.sy)
    obs.obs_sceneitem_defer_group_resize_end(group)
    add_opacity(group, r.opacity)
    return group
end

-- Manual mode: the group is built in client pixels (frame origin = 0,0) and OBS owns its
-- position/scale afterwards. Moving or scaling it in the preview is remembered per region.
local function add_region_manual(scene, base, r, T, scene_name, record)
    if not r.visible or r.w < 6 or r.h < 6 or r.w > 10006 or r.h > 10006 or r.cw <= 0 or r.ch <= 0 then return end
    local group = add_group(scene, PREFIX .. r.id:sub(1, 6))
    if not group then return end
    obs.obs_sceneitem_defer_group_resize_begin(group)
    local inner = obs.obs_sceneitem_group_get_scene(group)
    rect(inner, "dark", 0, 0, r.w, r.h)
    rect(inner, "light", 0, 0, r.w, 1)
    rect(inner, "light", 0, 0, 1, r.h)
    rect(inner, "middle", 1, 1, r.w - 2, r.h - 2)
    rect(inner, "dark", 2, 2, r.w - 4, r.h - 4)
    rect(inner, "black", 3, 3, r.w - 6, r.h - 6)
    add_crop_item(inner, base, r, T.sw, T.sh, r.cx, r.cy, r.cw, r.ch)
    obs.obs_sceneitem_defer_group_resize_end(group)
    add_opacity(group, r.opacity)
    local key = scene_name .. "/" .. r.id
    local o = overrides[key] or {x = T.ox + r.left * T.sx, y = T.oy + r.top * T.sy, sx = T.sx, sy = T.sy}
    place(group, o.x, o.y, o.sx, o.sy)
    record.items[#record.items + 1] = group
    record.groups[#record.groups + 1] = {item = group, key = key, x = o.x, y = o.y, sx = o.sx, sy = o.sy}
end

local function build(scene, base_item, base, p, T, guides, scene_name)
    local record = {items = {}, groups = {}, fresh = true}
    for _, r in ipairs(p.regions) do
        if cfg.manual then
            add_region_manual(scene, base, r, T, scene_name, record)
        else
            local group = add_region(scene, base, r, T)
            if group then record.items[#record.items + 1] = group end
        end
    end
    if guides then -- outline of the game area as the script understands it
        local group = add_group(scene, PREFIX .. "guia")
        if group then
            local inner = obs.obs_sceneitem_group_get_scene(group)
            local x, y, w, h = T.ox, T.oy, T.cw * T.sx, T.ch * T.sy
            rect(inner, "guide", x, y, w, 2); rect(inner, "guide", x, y + h - 2, w, 2)
            rect(inner, "guide", x, y, 2, h); rect(inner, "guide", x + w - 2, y, 2, h)
            record.items[#record.items + 1] = group
        end
    end
    for _, item in ipairs(record.items) do obs.obs_sceneitem_set_order(item, obs.OBS_ORDER_MOVE_TOP) end
    return record
end

-- ---------------------------------------------------------------- tick
local function find_base()
    if cfg.source ~= "" then return cfg.source end
    local sources = obs.obs_enum_sources()
    local found = ""
    if sources then
        for _, s in ipairs(sources) do
            if found == "" and obs.obs_source_get_id(s) == "game_capture" then found = obs.obs_source_get_name(s) end
        end
        obs.source_list_release(sources)
    end
    return found
end

local function manual_sig(p, name, sw, sh, T)
    local parts = {name, sw, sh, cfg.guides and 1 or 0}
    if cfg.guides then parts[#parts + 1] = string.format("%.2f,%.2f,%.4f,%.4f", T.ox, T.oy, T.sx, T.sy) end
    for _, r in ipairs(p.regions) do
        parts[#parts + 1] = string.format("%s:%d:%d:%d:%.5f:%.5f:%.5f:%.5f:%.2f:%.2f:%.2f:%.2f:%d", r.id, r.visible and 1 or 0, r.w, r.h,
            r.cropx, r.cropy, r.cropw, r.croph, r.cx, r.cy, r.cw, r.ch, r.opacity)
    end
    return table.concat(parts, "|")
end

-- Remember where the user dragged/scaled each group in the OBS preview.
local function poll_user(record)
    local pos, scale = obs.vec2(), obs.vec2()
    for _, g in ipairs(record.groups) do
        obs.obs_sceneitem_get_pos(g.item, pos)
        obs.obs_sceneitem_get_scale(g.item, scale)
        local moved = math.abs(pos.x - g.x) > 0.01 or math.abs(pos.y - g.y) > 0.01 or math.abs(scale.x - g.sx) > 1e-4 or math.abs(scale.y - g.sy) > 1e-4
        if moved then
            g.x, g.y, g.sx, g.sy = pos.x, pos.y, scale.x, scale.y
            -- the first reading after a build only adopts what OBS settled on
            if not record.fresh then overrides[g.key] = {x = g.x, y = g.y, sx = g.sx, sy = g.sy}; overrides_dirty = true end
        end
    end
    record.fresh = false
end
poll_user_ref = poll_user

local function apply()
    local want = packet and packet.enabled and packet.cw >= 1 and packet.ch >= 1 and packet.cw <= 8192 and packet.ch <= 8192
    local name = want and find_base() or ""
    local base = name ~= "" and obs.obs_get_source_by_name(name) or nil
    local sw, sh = 0, 0
    if base then sw, sh = obs.obs_source_get_width(base), obs.obs_source_get_height(base) end
    if not want or not base or sw < 1 or sh < 1 then
        clear_all()
        if base then obs.obs_source_release(base) end
        return
    end

    local seen = {}
    local m, crop = obs.matrix4(), obs.obs_sceneitem_crop()
    for _, scene_name in ipairs(scene_names) do
        local source = obs.obs_get_source_by_name(scene_name)
        local scene = source and obs.obs_scene_from_source(source)
        local item = scene and obs.obs_scene_find_source(scene, name)
        if scene and not item and not warned_nested[scene_name] and obs.obs_scene_find_source_recursive
            and obs.obs_scene_find_source_recursive(scene, name) then
            warned_nested[scene_name] = true
            log("AVISO: a Captura de jogo '" .. name .. "' esta dentro de um grupo na cena '" .. scene_name ..
                "'. Tire-a do grupo (clique direito > Desagrupar): sem isso nenhuma overlay e criada nessa cena.")
        end
        if item and obs.obs_sceneitem_visible(item) then
            obs.obs_sceneitem_get_draw_transform(item, m)
            obs.obs_sceneitem_get_crop(item, crop)
            if math.abs(m.x.y) < 1e-6 and math.abs(m.y.x) < 1e-6 and m.x.x > 0 and m.y.y > 0 then
                local kx, ky = sw / packet.cw, sh / packet.ch -- capture pixels per client pixel (1 when sizes match)
                local T = {
                    sw = sw, sh = sh, cw = packet.cw, ch = packet.ch,
                    sx = m.x.x * kx, sy = m.y.y * ky,
                    ox = m.t.x - crop.left * m.x.x + cfg.dx, oy = m.t.y - crop.top * m.y.y + cfg.dy,
                }
                local sig
                if cfg.manual then -- positions belong to the user: only content changes rebuild
                    sig = manual_sig(packet, name, sw, sh, T)
                else
                    sig = string.format("%s|%s|%d|%d|%.4f|%.4f|%.3f|%.3f|%d", packet.key, name, sw, sh, T.sx, T.sy, T.ox, T.oy, cfg.guides and 1 or 0)
                end
                seen[scene_name] = true
                local record = built[scene_name]
                local gone = record and not alive(record) -- user deleted a group, or undo/redo reloaded the scene
                if gone then
                    remove_items(record)
                    built[scene_name] = nil -- rebuild on the next tick, once OBS freed the old names
                end
                if cfg.manual and record and not gone and record.sig == sig then poll_user(record) end
                if gone then
                    -- wait for the next tick
                elseif record and record.sig ~= sig then
                    -- Remove now and rebuild on the next tick: OBS frees the old group names asynchronously.
                    if cfg.manual then poll_user(record) end -- keep the last user move
                    remove_items(record)
                    built[scene_name] = nil
                elseif not record then
                    record = build(scene, item, base, packet, T, cfg.guides, scene_name)
                    record.sig = sig
                    built[scene_name] = record
                    local line = string.format("scene=%s capture=%dx%d client=%dx%d box=(%.1f,%.1f) px/client=(%.4f,%.4f) crop=%d,%d,%d,%d regions=%d",
                        scene_name, sw, sh, packet.cw, packet.ch, T.ox, T.oy, T.sx, T.sy, crop.left, crop.top, crop.right, crop.bottom, #record.items)
                    if line ~= last_log then last_log = line; log(line) end
                end
            end
        end
        if source then obs.obs_source_release(source) end
    end
    for scene_name, record in pairs(built) do
        if not seen[scene_name] then remove_items(record); built[scene_name] = nil end
    end
    obs.obs_source_release(base)
end

local function tick()
    local now = os.time()
    if now - scene_refresh >= 1 then
        scene_refresh = now
        local names = {}
        for_each_scene(function(n) names[#names + 1] = n end)
        scene_names = names
    end
    local p = read_packet(cfg.path)
    if p then
        local age = now - p.heartbeat
        if p.version == 1 and age <= 5 and age >= -5 then packet, packet_time = p, now else packet = nil end
    elseif packet and now - packet_time > 5 then
        packet = nil
    end
    apply()
    if overrides_dirty and now - overrides_saved >= 1 then overrides_saved = now; save_overrides() end
end

-- ---------------------------------------------------------------- OBS script API
function script_description()
    return "Tibia Scarab Eye: desenha as overlays como grupos na cena. Modo manual: arraste e redimensione cada grupo no preview do OBS até ficar como no jogo; a posição fica salva. Modo automático: a posição vem do programa, calculada pela transformação real da Captura de jogo."
end

function script_defaults(settings)
    obs.obs_data_set_default_string(settings, "layout_path", default_path())
    obs.obs_data_set_default_int(settings, "dx", 0)
    obs.obs_data_set_default_int(settings, "dy", 0)
    obs.obs_data_set_default_bool(settings, "guides", false)
    obs.obs_data_set_default_bool(settings, "manual", true)
end

function script_properties()
    local props = obs.obs_properties_create()
    local list = obs.obs_properties_add_list(props, "source", "Captura do jogo (vazio = primeira Captura de jogo)", obs.OBS_COMBO_TYPE_LIST, obs.OBS_COMBO_FORMAT_STRING)
    obs.obs_property_list_add_string(list, "Automático", "")
    local sources = obs.obs_enum_sources()
    if sources then
        for _, s in ipairs(sources) do
            local id = obs.obs_source_get_id(s)
            if id == "game_capture" or id == "window_capture" or id == "monitor_capture" then
                local n = obs.obs_source_get_name(s)
                obs.obs_property_list_add_string(list, n, n)
            end
        end
        obs.source_list_release(sources)
    end
    obs.obs_properties_add_path(props, "layout_path", "Layout sincronizado pelo Tibia Scarab Eye", obs.OBS_PATH_FILE, "JSON (*.json)", nil)
    obs.obs_properties_add_bool(props, "manual", "Modo manual: posicionar as overlays dentro do OBS (a posição do programa só vale na 1ª vez)")
    obs.obs_properties_add_button(props, "reset", "Redefinir posições manuais", function()
        clear_all()
        overrides = {}
        save_overrides()
        return true
    end)
    obs.obs_properties_add_bool(props, "guides", "Mostrar guia da área do jogo (diagnóstico)")
    obs.obs_properties_add_int(props, "dx", "Ajuste fino X (pixels do canvas)", -500, 500, 1)
    obs.obs_properties_add_int(props, "dy", "Ajuste fino Y (pixels do canvas)", -500, 500, 1)
    return props
end

function script_update(settings)
    cfg.source = obs.obs_data_get_string(settings, "source")
    cfg.path = obs.obs_data_get_string(settings, "layout_path")
    cfg.dx = obs.obs_data_get_int(settings, "dx")
    cfg.dy = obs.obs_data_get_int(settings, "dy")
    cfg.guides = obs.obs_data_get_bool(settings, "guides")
    cfg.manual = obs.obs_data_get_bool(settings, "manual")
    if cfg.path == "" then cfg.path = default_path() end
    clear_all() -- next tick rebuilds with the new settings
    if overrides_dirty then save_overrides() end
    load_overrides()
end

local function on_event(event)
    if event == obs.OBS_FRONTEND_EVENT_EXIT or event == obs.OBS_FRONTEND_EVENT_SCENE_COLLECTION_CHANGING then
        clear_all()
        if overrides_dirty then save_overrides() end
        for_each_scene(function(_, scene) purge(scene) end)
    end
end

function script_load(settings)
    for key, color in pairs(PALETTE) do
        local s = obs.obs_data_create()
        obs.obs_data_set_int(s, "color", color)
        obs.obs_data_set_int(s, "width", 1)
        obs.obs_data_set_int(s, "height", 1)
        colors[key] = obs.obs_source_create_private("color_source", PREFIX .. key, s)
        obs.obs_data_release(s)
    end
    for_each_scene(function(_, scene) purge(scene) end) -- leftovers from a previous session or crash
    obs.obs_frontend_add_event_callback(on_event)
    obs.timer_add(tick, 100)
end

function script_unload()
    obs.timer_remove(tick)
    obs.obs_frontend_remove_event_callback(on_event)
    clear_all()
    if overrides_dirty then save_overrides() end
    for_each_scene(function(_, scene) purge(scene) end)
    for _, s in pairs(colors) do obs.obs_source_release(s) end
    colors = {}
end

