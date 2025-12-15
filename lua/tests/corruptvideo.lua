if not CLIENT then return end
print("AIChaos Mirror Mode Loaded")

local hook_name = "AIChaos_MirrorMode"
local rt_name = "AIChaos_MirrorRT"
local rt, mat

-- Initialize safely
timer.Simple(0.1, function()
    rt = GetRenderTarget(rt_name, ScrW(), ScrH(), false)
    mat = CreateMaterial("AIChaos_MirrorMat_" .. CurTime(), "UnlitGeneric", {
        ["$basetexture"] = rt_name,
        ["$vertexcolor"] = 1,
        ["$vertexalpha"] = 1,
        ["$ignorez"] = 1
    })
end)

-- Capture world to render target
hook.Add("PreDrawViewModel", hook_name, function()
    if not rt or not mat then return end
    
    local old_rt = render.GetRenderTarget()
    
    render.SetRenderTarget(rt)
    render.Clear(0, 0, 0, 255, true, true)
    
    -- Render current view
    render.RenderView({
        origin = EyePos(),
        angles = EyeAngles(),
        x = 0, y = 0,
        w = ScrW(), h = ScrH(),
        drawviewmodel = false
    })
    
    render.SetRenderTarget(old_rt)
end)

-- Draw flipped version
hook.Add("PostDrawHUD", hook_name, function()
    if not mat then return end
    
    cam.Start2D()
    surface.SetDrawColor(255, 255, 255, 255)
    surface.SetMaterial(mat)
    -- Flip horizontally with UV coords
    surface.DrawTexturedRectUV(0, 0, ScrW(), ScrH(), 1, 0, 0, 1)
    cam.End2D()
end)

-- 6. Mirror Keyboard Controls (Invert A/D)
hook.Add("CreateMove", hook_name, function(cmd)
    cmd:SetSideMove(-cmd:GetSideMove())
end)

-- 7. Mirror Mouse Controls (Invert Mouse X)
hook.Add("InputMouseApply", hook_name, function(cmd, x, y, ang)
    local view_ang = cmd:GetViewAngles()
    local pitch = GetConVar("m_pitch"):GetFloat()
    local yaw = GetConVar("m_yaw"):GetFloat()
    
    -- Standard is -=, so += inverts it
    view_ang.y = view_ang.y + (x * yaw)
    view_ang.p = view_ang.p + (y * pitch)
    
    -- Clamp Pitch just in case
    if view_ang.p > 89 then view_ang.p = 89 end
    if view_ang.p < -89 then view_ang.p = -89 end
    
    cmd:SetViewAngles(view_ang)
    return true
end)