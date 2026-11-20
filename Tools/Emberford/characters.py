"""Weighted fantasy characters built over a shared rest pose."""
from math import pi, sin
from mathutils import Euler, Vector
import bpy
from structures import weapon


def dimensions(kind):
    return {"brute":(1.38,1.34),"goblin":(.74,.87),"sentinel":(1.26,1.17),
            "wraith":(1.08,.93),"smith":(1.0,1.09),"merchant":(.96,.92),
            "scout":(1.0,.94)}.get(kind,(1.0,1.0))


def joints(kind):
    h,w=dimensions(kind)
    p=lambda x,y,z:(x*w,y*w,z*h)
    out={"root":(p(0,0,0),p(0,0,.25),None),
         "hips":(p(0,0,.91),p(0,0,1.12),"root"),
         "spine":(p(0,0,1.12),p(0,0,1.56),"hips"),
         "head":(p(0,0,1.59),p(0,0,1.99),"spine")}
    for sign,s in ((-1,"R"),(1,"L")):
        out["upper_arm."+s]=(p(sign*.33,0,1.51),p(sign*.52,0,1.23),"spine")
        out["forearm."+s]=(p(sign*.52,0,1.23),p(sign*.63,-.035,1.00),"upper_arm."+s)
        out["hand."+s]=(p(sign*.63,-.035,1.00),p(sign*.67,-.06,.91),"forearm."+s)
        out["thigh."+s]=(p(sign*.185,0,.93),p(sign*.205,-.045,.53),"hips")
        out["shin."+s]=(p(sign*.205,-.045,.53),p(sign*.225,0,.16),"thigh."+s)
        out["foot."+s]=(p(sign*.225,0,.16),p(sign*.228,-.21,.095),"shin."+s)
    return out


def character(b,kind,hostile=False):
    h,w=dimensions(kind)
    armor=kind in ("knight","paladin","sentinel")
    caster=kind in ("mage","wraith")
    civilian=kind in ("smith","merchant","scout")
    skin={"goblin":"Goblin Skin","brute":"Brute Skin","merchant":"Skin Deep",
          "wraith":"Skin Ash","sentinel":"Basalt"}.get(kind,"Skin Ash" if hostile else "Skin Warm")
    metal="Black Iron" if hostile else "Iron"
    edge="Red Iron" if hostile else "Steel"
    accent="Banner Red" if hostile else "Banner Teal"
    cloth="Cloak Violet" if hostile else "Cloak Blue"
    jacket={"smith":"Oak Edge","merchant":"Linen","scout":"Banner Teal","brute":"Leather",
            "goblin":"Leather","sentinel":"Basalt"}.get(kind,metal if armor else cloth)
    p=lambda x,y,z:(x*w,y*w,z*h)
    size=lambda x,y,z:(x*w,y*w,z*h)
    def box(x,y,z,sx,sy,sz,mat,bone,ch=.028,rot=(0,0,0)):
        b.box(p(x,y,z),size(sx,sy,sz),mat,ch,rot,bone)
    def oval(x,y,z,sx,sy,sz,mat,bone,sides=10):
        b.oval(p(x,y,z),size(sx,sy,sz),mat,bone,sides)
    def beam(start,end,r,mat,bone,t=None,sides=8):
        b.beam(p(*start),p(*end),r*w,mat,None if t is None else t*w,sides,bone)
    def plate(points,y,dep,mat,bone):
        b.plate([(x*w,z*h) for x,z in points],y*w,dep*w,mat,bone)

    oval(0,0,1.28,.342,.183,.322,jacket,"spine",12)
    oval(0,0,.99,.266,.164,.178,metal if armor else "Leather","hips")
    box(0,-.035,1.098,.52,.357,.093,"Dark Wood","hips")
    box(0,-.221,1.10,.146,.054,.132,"Gold" if not hostile else edge,"hips")
    box(0,-.253,1.10,.064,.025,.058,"Jade" if not hostile else "Violet","hips",.01)
    beam((0,.01,1.51),(0,-.018,1.71),.096,skin,"head",.083)

    # Distinct garment silhouettes stay readable at small on-screen sizes.
    if armor:
        plate([(-.21,1.28),(-.245,1.43),(-.166,1.55),(.166,1.55),(.245,1.43),(.21,1.28),(0,1.20)],
              -.141,.119,edge,"spine")
        plate([(-.14,1.31),(-.17,1.44),(0,1.51),(.17,1.44),(.14,1.31),(0,1.24)],
              -.213,.050,metal,"spine")
        plate([(-.065,1.39),(0,1.30),(.065,1.39),(0,1.48)],-.247,.033,"Amber" if kind=="paladin" else accent,"spine")
    elif caster:
        plate([(-.25,1.47),(-.29,1.09),(-.24,.865),(.24,.865),(.29,1.09),(.25,1.47)],
              -.117,.125,cloth,"spine")
        plate([(-.057,.87),(.061,.87),(.10,1.48),(-.079,1.48)],-.188,.035,"Gold" if not hostile else accent,"spine")
        box(0,-.213,1.31,.115,.045,.115,"Jade" if not hostile else "Violet","spine",.022,(0,pi/4,0))
    else:
        plate([(-.19,.965),(.19,.965),(.133,1.48),(-.132,1.50)],-.147,.075,
              "Leather" if kind!="merchant" else "Oak Edge","spine")
        if kind in ("rogue","scout","brute"):
            beam((-.25,-.147,1.49),(.23,-.198,1.07),.049,accent,"spine",.044,4)
    if kind in ("paladin","mage","wraith","knight"):
        plate([(-.21,1.55),(-.27,1.22),(-.36,.80),(-.18,.69),(.04,.78),(.27,.69),(.34,.82),(.24,1.55)],
              .166,.082,accent if armor else cloth,"spine")

    # Face and headwear use overlapping sculpted volumes with modeled profiles.
    oval(0,-.015,1.777,.157,.126,.199,skin,"head",12)
    oval(0,-.032,1.689,.114,.103,.094,skin,"head",10)
    box(0,-.137,1.704,.106,.022,.012,"Hair","head",.004)
    oval(0,-.139,1.767,.036,.053,.057,skin,"head",8)
    for side in (-1,1):
        oval(side*.148,-.004,1.767,.033,.035,.061,skin,"head",8)
        if kind=="goblin":
            beam((side*.119,-.003,1.768),(side*.305,.018,1.868),.075,skin,"head",.009,5)
        box(side*.061,-.131,1.806,.076,.035,.033,"Violet" if hostile and not civilian else "Eye","head",.008)
        box(side*.062,-.152,1.805,.025,.014,.026,"Black","head",.006)
        box(side*.064,-.137,1.839,.091,.038,.026,"Silver Hair" if kind=="mage" and not hostile else "Hair",
            "head",.006,(0,(-side)*(.12 if hostile else .06),0))
    if armor:
        oval(0,.023,1.893,.185,.147,.155,metal,"head",12)
        box(0,-.105,1.911,.279,.146,.074,edge,"head",.028)
        for s in (-1,1):
            box(s*.154,-.004,1.795,.074,.123,.203,metal,"head",.027,(0,s*.12,0))
            if hostile:
                beam((s*.133,.005,1.94),(s*.224,.025,2.164),.063,edge,"head",.009,6)
        if kind=="paladin":
            plate([(-.07,1.91),(0,1.98),(.07,1.91),(0,1.855)],-.187,.04,"Amber" if not hostile else "Violet","head")
    elif caster or kind=="rogue":
        oval(0,.027,1.895,.184,.151,.153,cloth,"head",12)
        for s in (-1,1):
            beam((0,-.107,1.989),(s*.168,-.078,1.772),.039,cloth,"head",.027,6)
        if caster:
            beam((0,.048,1.952),(.073,.063,2.142),.128,cloth,"head",.019,8)
        if kind=="rogue":
            box(0,-.135,1.704,.231,.110,.114,cloth,"head",.033)
    else:
        oval(0,.028,1.901,.158,.133,.086,
             "Silver Hair" if kind in ("smith","brute") else "Hair","head",10)
        if kind=="smith":
            oval(0,-.054,1.678,.107,.10,.087,"Silver Hair","head",8)
        if kind=="merchant":
            box(0,.00,1.919,.355,.286,.076,accent,"head",.038)

    for s,suffix in ((-1,"R"),(1,"L")):
        upper,fore,hand=["%s.%s"%(k,suffix) for k in ("upper_arm","forearm","hand")]
        thigh,shin,foot=["%s.%s"%(k,suffix) for k in ("thigh","shin","foot")]
        beam((s*.178,0,.955),(s*.205,-.045,.53),.119,"Dark Wood" if civilian else cloth,thigh,.083,8)
        oval(s*.21,-.02,.325,.091,.106,.22,metal if armor else "Leather",shin,9)
        oval(s*.227,-.084,.118,.104,.184,.107,"Dark Wood",foot,10)
        box(s*.227,-.102,.048,.202,.334,.063,edge if armor else "Leather",foot,.03)
        if armor:
            oval(s*.207,-.098,.55,.105,.07,.117,edge,shin,8)
        beam((s*.33,0,1.51),(s*.523,0,1.235),.108,jacket,upper,.087)
        beam((s*.52,0,1.235),(s*.628,-.03,1.0),.085,skin if civilian or kind=="brute" else metal,fore,.068)
        oval(s*.658,-.048,.95,.074,.071,.111,skin if civilian or kind in ("brute","goblin") else "Leather",hand,9)
        if armor or hostile:
            oval(s*.375,.006,1.49,.19 if armor else .144,.148,.126,edge,upper,9)
            if hostile and b.lod<2:
                beam((s*.418,.016,1.52),(s*.576,.035,1.694),.083,metal,upper,.012,6)
        elif caster:
            oval(s*.362,0,1.485,.126,.126,.097,accent,upper,9)
        if b.lod<2:
            box(s*.605,-.053,1.065,.16,.154,.079,"Gold" if kind=="paladin" else metal,fore,.024)

    def grip(item, side, tilt, roll, handle_center):
        wrist = "hand.R" if side == -1 else "hand.L"
        palm = Vector(p(side*.655, -.066, .951))
        pose = (tilt, side * roll, side * .085)
        # The exported handle midpoint coincides with the skinned knuckles at every LOD.
        pivot = palm - Euler(pose).to_matrix() @ Vector((0, 0, handle_center))
        weapon(b, item, pivot, wrist, pose)
        knuckles = "Leather" if kind in ("knight", "paladin", "rogue") else skin
        for finger in (-1, 0, 1):
            box(side*.659 + finger*.030, -.113, .944, .038, .049, .082, knuckles, wrist, .012)

    item={"knight":("sword",1.18,.15,.105),"mage":("staff",.13,.26,.80),
          "rogue":("dagger",1.02,.23,.105),"paladin":("runeblade",1.39,.20,.105),
          "brute":("club",1.21,.22,.184),"goblin":("dagger",1.11,.22,.105),
          "wraith":("staff",.19,.28,.78),"sentinel":("maul",1.28,.23,.19)}.get(kind)
    if item:
        grip(item[0], -1, *item[1:])
    if kind in ("knight","paladin"):
        x=.66
        beam((x-.072,-.085,.949),(x+.072,-.085,.949),.028,"Dark Wood","hand.L",sides=6)
        plate([(x-.19,.96),(x-.18,1.29),(x,1.36),(x+.18,1.29),(x+.19,.96),(x,.75)],
              -.105,.101,edge,"hand.L")
        plate([(x-.145,.97),(x-.14,1.255),(x,1.30),(x+.14,1.255),(x+.145,.97),(x,.814)],
              -.168,.047,accent,"hand.L")
        plate([(x,1.225),(x-.081,1.08),(x,.920),(x+.081,1.08)],-.20,.030,
              "Bone" if hostile else "Gold","hand.L")
    if kind=="rogue":
        grip("dagger", 1, .94, .24, .105)
    if kind in ("brute", "goblin"):
        for side in (-1, 1):
            beam((side*.066,-.119,1.691),(side*.089,-.154,1.761),
                 .027 if kind=="brute" else .018,"Bone","head",.009,6)
        if kind=="brute":
            plate([(-.31,1.32),(-.23,1.51),(.24,1.48),(.18,1.31),(-.05,1.22)],
                  -.142,.069,"Dark Wood","spine")
            box(.12,-.225,1.214,.193,.061,.112,"Red Iron","spine",.021)
            for side in (-1, 1):
                plate([(side*.072,1.777),(side*.126,1.792),(side*.114,1.735),(side*.086,1.726)],
                      -.117,.028,"Banner Red","head")
            beam((-.279,-.160,1.53),(-.125,-.215,1.34),.044,"Bone","spine",.028,6)
        else:
            plate([(-.17,1.858),(-.12,1.949),(.088,1.936),(.091,1.867),(-.032,1.848)],
                  -.144,.057,"Red Iron","head")
            beam((-.234,-.114,1.440),(.203,-.190,1.149),.057,"Oak Edge","spine",.046,6)
            if b.lod < 2:
                for x in (-.113, -.061, -.008):
                    box(x,-.177,1.892,.025,.021,.027,"Iron","head",.007)
    if kind=="wraith":
        for side in (-1,1):
            plate([(side*.23,1.56),(side*.51,1.55),(side*.42,1.19),(side*.29,1.29)],
                  .048,.051,"Cloak Violet","spine")
            beam((side*.25,-.09,1.51),(side*.45,-.068,1.61),.048,"Black Iron","spine",.012,5)
    if kind=="sentinel":
        box(0,-.234,1.403,.225,.088,.225,"Basalt","spine",.031,(0,pi/4,0))
        box(0,-.288,1.403,.142,.053,.142,"Jade","spine",.020,(0,pi/4,0))
        for side, suffix in ((-1,"R"),(1,"L")):
            arm = "upper_arm." + suffix
            plate([(side*.29,1.59),(side*.42,1.669),(side*.565,1.588),(side*.514,1.442),(side*.316,1.474)],
                  -.104,.13,"Basalt",arm)
            plate([(side*.372,1.60),(side*.43,1.626),(side*.471,1.578),(side*.409,1.541)],
                  -.187,.028,"Jade",arm)
            box(side*.081,-.158,1.807,.078,.032,.039,"Jade","head",.008)
    if kind in ("smith","scout","merchant"):
        box(.19,-.11,.98,.149,.186,.165,"Oak","hips")
        box(.19,-.216,1.028,.137,.048,.069,"Gold","hips",.012)


def rig(kind, name, collection):
    data = bpy.data.armatures.new(name+" Skeleton")
    obj = bpy.data.objects.new(name+" Skeleton",data)
    collection.objects.link(obj)
    bpy.context.view_layer.objects.active=obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for key,(start,end,parent) in joints(kind).items():
        bone=data.edit_bones.new(key)
        bone.head,bone.tail=start,end
        if parent:
            bone.parent=data.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    return obj


def animate(armature):
    scene=bpy.context.scene
    scene.render.fps=30
    scene.frame_end=61
    animator=armature.animation_data_create()
    for name in ("Idle","Walk"):
        action=bpy.data.actions.new(name)
        animator.action=action
        action.use_fake_user=True
        for frame in (1,11,21,31,41,51,61):
            cycle=(frame-1)/60*(2*pi if name=="Idle" else 4*pi)
            for pb in armature.pose.bones:
                pb.rotation_mode="XYZ"
                pb.rotation_euler=(0,0,0)
                pb.location=(0,0,0)
                if pb.name=="spine":
                    pb.rotation_euler=(.025*sin(cycle),0,.034*sin(cycle) if name=="Walk" else .012*sin(cycle))
                if pb.name=="hips":
                    pb.location.z=.024*abs(sin(cycle)) if name=="Walk" else .009*sin(cycle)
                if name=="Walk" and "." in pb.name:
                    limb,side=pb.name.split(".")
                    t=sin(cycle)*(1 if side=="L" else -1)
                    wave={"thigh":.39*t,"shin":.39*max(0,-t),"foot":-.11*t,"upper_arm":-.27*t}.get(limb,0)
                    pb.rotation_euler.x=wave
                pb.keyframe_insert(data_path="rotation_euler",frame=frame,group=pb.name)
                if pb.name=="hips":
                    pb.keyframe_insert(data_path="location",frame=frame,group=pb.name)
        track=animator.nla_tracks.new()
        track.name=name
        track.strips.new(name,1 if name=="Idle" else 63,action)
        animator.action=None

    scene.frame_set(1)
