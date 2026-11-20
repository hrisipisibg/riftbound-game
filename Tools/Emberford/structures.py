"""Modular architecture and weapons for Emberford and the Briar Watch."""
from math import pi, sin, cos, atan2, hypot
from meshkit import Build
import random


def coursed_wall(b,w,depth,height,seed,broken=False,center=(0,0,0),angle=0):
    rng=random.Random(seed)
    ox,oy,oz=center
    courses=3 if b.lod==2 else round(height/.34)
    stone_h=height/courses
    pitch=.71 if b.lod<2 else 1.1
    for row in range(courses):
        z=oz+(row+.5)*stone_h
        left=-w/2+(row%2)*pitch*.45
        if broken and row>1:
            left+=max(0,(row-1))*pitch*.36
        right=w/2-(0 if not broken else max(0,row-2)*pitch*.27)
        if left>-w/2 and not broken:
            extent=left+w/2
            x=-w/2+extent*.5
            b.box((ox+x*cos(angle),oy+x*sin(angle),z),(extent-.014,depth,stone_h-.018),
                  "Slate",.025,(0,0,angle))
        while left < right-.15:
            length=min(right-left,rng.uniform(pitch*.75,pitch*1.25))
            x=left+length*.5
            if not (broken and row>3 and abs(x)<.43 and row<6):
                b.box((ox+x*cos(angle),oy+x*sin(angle),z+rng.uniform(-.019,.019)),
                      (length-.022,depth+rng.uniform(-.04,.08),stone_h-.029),
                      rng.choices(("Limestone","Pale Stone","Slate"),(6,2,2))[0],.047,(0,0,angle))
            left+=length


def window(b,x=0,y=0,z=1.4,width=1.1,height=1.35,broken=False,skin="Oak Edge"):
    if not broken:
        b.box((x,y+.057,z),(width,.13,height),"Dark Wood",.02)
        panes=2 if b.lod<2 else 1
        for i in range(panes):
            a=x+(i-(panes-1)/2)*width*.38
            b.box((a,y-.035,z),(width*.32 if panes==2 else width*.70,.04,height*.75),"Amber",.01)
    else:
        b.plate([(x-width*.39,z-height*.41),(x-width*.08,z-height*.40),(x-width*.31,z-height*.16)],
                y,.028,"Slate Edge")
        b.plate([(x+width*.39,z+height*.40),(x+width*.19,z+height*.41),(x+width*.35,z+height*.14)],
                y,.028,"Slate Edge")
    for side in (-1,1):
        b.box((x+side*width*.5,y-.09,z),(.14,.20,height+.23),skin,.041,(0,side*.028 if broken else 0,0))
    for side in (-1,1):
        b.box((x,y-.12,z+side*height*.5),(width+.29,.32,.16),skin,.025)
    if not broken and b.lod<2:
        b.box((x,y-.13,z),(.065,.15,height),skin,.012)
        b.box((x,y-.13,z),(width,.15,.065),skin,.012)
    if broken:
        b.beam((x-width*.47,y-.14,z+.06),(x+.06,y-.16,z-.19),.034,skin,.017,4)


def door(b,x=0,y=0,z=0,width=1.2,height=2.05,broken=False):
    for side in (-1,1):
        b.box((x+width*.54*side,y,z+height*.5),(.18,.28,height+.21),"Oak Edge",.045)
    b.box((x,y-.055,z+height),(width+.38,.33,.19),"Oak Edge",.045,(0,-.035,0))
    if not broken:
        b.box((x,y+.08,z+height*.50),(width,.19,height),"Dark Wood",.025)
    for j in range(5 if b.lod<2 else 3):
        count=5 if b.lod<2 else 3
        if broken and j in (1,2):
            continue
        board=width/count
        length=height*(.38+.12*(j%3)) if broken else height*.95
        b.box((x+(j-(count-1)/2)*board,y-.031,z+length*.5),(board-.032,.18,length),
              "Oak" if j%2 else "Oak Edge",.024,(0,(j-2)*.043 if broken else 0,0))
    for h in (.46,1.46):
        if broken and h>1:
            continue
        b.box((x if not broken else x+width*.32,y-.15,z+h),(width*.90 if not broken else width*.29,.07,.11),"Iron",.012)
    if not broken:
        b.beam((x+.36,y-.22,z+height*.53),(x+.36,y-.29,z+height*.53),.074,"Gold",sides=8)


def roof(b,w,depth,eaves,rise,seed,red=False):
    r=random.Random(seed)
    tiles=("Ember Tile","Ember Edge") if red else ("Slate","Slate Edge")
    half=w/2
    theta=atan2(rise,half)
    length=hypot(half,rise)
    for side in (-1,1):
        b.box((side*half*.5,0,eaves+rise*.5),(length+.17,depth,.18),"Dark Wood",.048,(0,side*theta,0))
        rows=9 if b.lod==0 else 5 if b.lod==1 else 2
        columns=max(2,round(depth/(.57 if b.lod==0 else 1.0 if b.lod==1 else 2.0)))
        for row in range(rows):
            f=(row+.52)/rows
            for col in range(columns):
                y=-depth/2+(col+.5)*depth/columns
                if row%2 and 0<col<columns-1:
                    y+=depth/columns*.19
                b.box((side*half*(1-f),y,eaves+rise*f+.135+r.uniform(-.028,.028)),
                      (length/rows*1.11,depth/columns*.94,.09),tiles[r.randrange(2) if r.random()<.25 else 0],
                      .030,(0,side*theta+r.uniform(-.045,.045),r.uniform(-.03,.03)))
        for y in (-depth*.51,depth*.51):
            b.beam((side*(half+.12),y,eaves-.04),(0,y,eaves+rise+.16),.147,"Oak Edge",.11,6)
    b.beam((0,-depth*.55,eaves+rise+.12),(0,depth*.55,eaves+rise+.12),.124,tiles[1],sides=8)


def sign(b,x,y,z,accent="Banner Teal"):
    b.beam((x,y+.33,z+.41),(x,y-.12,z+.41),.045,"Iron")
    b.plate([(x-.4,z+.26),(x-.33,z-.17),(x,z-.29),(x+.33,z-.17),(x+.4,z+.26)],y-.14,.10,"Oak Edge")
    b.plate([(x-.19,z+.16),(x-.19,z-.058),(x,z-.19),(x+.19,z-.058),(x+.19,z+.16)],y-.205,.04,accent)
    b.plate([(x,z+.14),(x-.10,z+.005),(x,z-.13),(x+.10,z+.005)],y-.24,.035,"Gold")


def banner(b,x,y,z,accent="Banner Teal",scale=1):
    b.beam((x-.52*scale,y,z),(x+.52*scale,y,z),.06*scale,"Oak Edge")
    outline=[(-.41,0),(-.38,-.93),(-.16,-1.13),(0,-.97),(.23,-1.17),(.38,-.97),(.41,0)]
    b.plate([(x+a*scale,z+c*scale) for a,c in outline],y-.08,.05,accent)
    b.plate([(x,z-.17*scale),(x-.15*scale,z-.48*scale),(x,z-.76*scale),(x+.15*scale,z-.48*scale)],
            y-.121,.034,"Gold" if accent=="Banner Teal" else "Bone")


def house(b,style,seed):
    spec={"house":(4.6,4.1,2.8,1.8),"inn":(6.8,5.1,4.4,2.1),
          "smith":(5.1,4.5,2.8,1.50),"chapel":(5.5,6.3,3.9,2.25),"hut":(3.7,3.4,2.05,1.25)}
    w,d,h,rise=spec[style]
    hostile=style=="hut"
    wall="Oak" if hostile else "Plaster"
    b.box((0,0,.17),(w+.10,d+.10,.34),"Basalt",.075)
    b.box((0,0,h*.5+.25),(w,d,h-.24),wall,.065)
    front=-d*.5
    b.mesh([(-w*.5,front,h),(w*.5,front,h),(0,front,h+rise),(-w*.5,d*.5,h),(w*.5,d*.5,h),(0,d*.5,h+rise)],
           [(0,1,2),(5,4,3)],wall)
    spacing=.75 if hostile else 1.05
    for z in (.43,h*.53 if style=="inn" else h-.24,h):
        for y in (-d*.5,d*.5):
            b.box((0,y,z),(w+.13,.25,.19),"Dark Wood" if hostile else "Oak Edge",.041)
        for side in (-1,1):
            b.box((side*w*.5,0,z),(.21,d+.12,.20),"Oak Edge",.034)
    for side in (-1,1):
        for y in (-d*.5,d*.5):
            b.box((side*(w*.5-.10),y,h*.51),(.26,.28,h-.04),"Oak Edge",.06,(0,side*.032,0))
        if not hostile or b.lod==0:
            b.beam((side*(w*.50-.22),front-.14,.46),(side*(w*.50-spacing),front-.14,h*.51),.068,"Oak Edge",.072,4)
        if style!="chapel" and not hostile:
            window(b,side*w*.29,front-.03,1.57,.74,.98)
        if style=="inn":
            window(b,side*w*.30,front-.04,3.36,1.06,1.23)
            balcony=(side*w*.26,front-.36,2.65)
            b.box(balcony,(1.69,.77,.17),"Oak",.04)
            b.box((balcony[0],front-.74,2.88),(1.68,.10,.12),"Oak Edge",.024)
            for n in (-1,0,1):
                b.box((balcony[0]+n*.56,front-.74,2.77),(.10,.10,.31),"Oak Edge",.018)
    door(b,0,front-.085,.29,1.57 if style=="chapel" else 1.12,2.27 if style=="chapel" else 1.98)
    b.box((0,front-.19,.21),(1.68,.95,.30),"Limestone",.065)
    b.box((0,front-.51,.11),(1.79,.59,.20),"Pale Stone",.048)
    roof(b,w+.87,d+.87,h+.055,rise,seed,style in ("hut","smith"))
    if style in ("inn","house","smith"):
        x=w*.27
        b.box((x,d*.20,h+rise*.62),(.71,.76,1.88),"Limestone",.11)
        b.box((x,d*.20,h+rise*.62+.94),(.92,.94,.22),"Basalt",.075)
        sign(b,(-w*.40 if style!="smith" else w*.42),front-.44,2.7,"Banner Teal")
    if style=="chapel":
        b.box((0,d*.17,h+1.05),(1.52,1.58,2.8),"Limestone",.11)
        roof(b,1.98,2.02,h+2.5,1.27,seed)
        window(b,0,d*.17-.80,h+1.52,.81,1.04,skin="Pale Stone")
        banner(b,w*.32,front-.33,3.19)
        banner(b,-w*.32,front-.33,3.19)
    if hostile:
        for side in (-1,1):
            b.beam((side*(w*.46),front-.22,.18),(side*(w*.53),front-.22,h+.58),.14,"Dark Wood",.055)
            b.beam((side*(w*.48),front-.24,h+.35),(side*(w*.57),front-.26,h+.75),.089,"Bone",.023)
        banner(b,w*.30,front-.26,1.96,"Banner Red",.67)


def arch(b,broken,seed):
    rng=random.Random(seed)
    for side in (-1,1):
        for row in range(6 if b.lod<2 else 3):
            z=(row+.5)*(0.35 if b.lod<2 else .70)
            if broken and side>0 and z>1.62:
                break
            b.box((side*1.06,0,z),(.62,.88,.32 if b.lod<2 else .66),
                  rng.choice(("Slate","Limestone","Pale Stone")),.055,(0,rng.uniform(-.09,.09),0))
    wedges=9 if b.lod<2 else 5
    for j in range(wedges if not broken else wedges//2+1):
        theta=.04+(pi-.08)*(j+.5)/wedges
        if broken:
            theta=pi-theta
        r=1.03
        b.box((cos(theta)*r,0,2.0+sin(theta)*r),(.51,.81,.45),
              "Pale Stone" if j==wedges//2 else "Limestone",.055,(0,pi/2-theta,0))


def debris(b,seed,collapsed=False):
    rng=random.Random(seed)
    if collapsed:
        coursed_wall(b,4.3,.70,2.55,seed,True,(0,.52,0))
        for i in range(6 if b.lod==0 else 3):
            x=-1.75+i*.67
            b.beam((x,.49,1.13+rng.random()*.51),(x+rng.uniform(-.4,.5),-2.1,.13),.127,"Oak",.085,4)
    for i in range((38 if collapsed else 22)//(b.lod+1)):
        x,y=rng.uniform(-1.95,1.95),rng.uniform(-1.7,1.4)
        k=rng.uniform(.15,.52)
        b.box((x,y,k*.34),(k*rng.uniform(1.1,2.0),k,k*.62),
              rng.choice(("Limestone","Pale Stone","Oak","Slate")),.048,
              (rng.uniform(-.25,.28),rng.uniform(-.42,.32),rng.uniform(0,pi*2)))


def palisade(b,seed):
    r=random.Random(seed)
    for i in range(7):
        x=(i-3)*.53
        h=2.8+r.uniform(-.27,.27)
        b.beam((x,r.uniform(-.06,.06),.02),(x,0,h-.30),.29,"Oak" if i%2 else "Dark Wood",.235)
        b.beam((x,0,h-.34),(x-.05,.03,h),.235,"Oak Edge",.017)
    for z in (.72,1.70):
        b.box((0,.24,z),(4.02,.29,.28),"Oak Edge",.055,(0,.025,0))
    for side in (-1,1):
        b.beam((side*1.43,.21,1.85),(side*1.70,1.14,.05),.125,"Dark Wood",.117)


def watchtower(b,seed,stone=False):
    for x in (-1.32,1.32):
        for y in (-1.21,1.21):
            b.beam((x*1.13,y*1.12,0),(x*.88,y*.88,4.04),.145,"Dark Wood",.114)
            for z in (1.51,3.10):
                b.box((x,y,z),(.34,.33,.11),"Iron",.025)
        b.beam((x,-1.20,.50),(x,1.13,3.65),.097,"Oak",.087,4)
        b.beam((x,1.20,.58),(x,-1.13,3.65),.097,"Oak",.087,4)
    if stone:
        b.box((0,0,.13),(3.69,3.54,.26),"Basalt",.12)
        b.box((0,0,1.71),(2.65,2.52,3.07),"Limestone",.11)
        for side in (-1,1):
            coursed_wall(b,3.03,.37,3.43,seed+side,center=(0,side*1.31,0))
            coursed_wall(b,2.62,.36,3.43,seed+side*2,center=(side*1.31,0,0),angle=pi*.5)
        door(b,0,-1.56,.14,.99,1.89)
        window(b,0,1.56,2.58,.51,.62,skin="Pale Stone")
    for x in (-1.36,1.36):
        for y in (-1.24,1.24):
            b.beam((x,y,3.45),(x,y,5.19),.108,"Oak Edge",.098,5)
            if b.lod < 2:
                b.beam((x,y,4.33),(x*.69,y,5.08),.082,"Dark Wood",.076,5)
    for y in (-1.24,1.24):
        b.beam((-1.43,y,4.97),(1.43,y,4.97),.13,"Oak Edge",sides=5)
    for i in range(9 if b.lod<2 else 5):
        n=9 if b.lod<2 else 5
        b.box((0,(i-(n-1)/2)*2.82/n,3.50),(3.14,2.82/n-.022,.19),"Oak Edge",.039)
    for y in (-1.45,1.45):
        b.box((0,y,3.85),(3.19,.15,.72),"Oak",.05)
    roof(b,3.60,3.25,4.91,1.04,seed,not stone)
    banner(b,.91,-1.74,4.48,"Banner Teal" if stone else "Banner Red",.83)
    for side in (-1,1):
        b.beam((side*.35,-2.48,.10),(side*.35,-1.28,3.56),.069,"Oak Edge",sides=4)
    for i in range(11 if b.lod<2 else 7):
        n=11 if b.lod<2 else 7
        f=i/(n-1)
        b.beam((-.36,-2.48+1.2*f,.12+3.35*f),(.36,-2.48+1.2*f,.12+3.35*f),.055,"Oak Edge",sides=5)


def stash(b,seed,barrel=False):
    if barrel:
        for i in range(12 if b.lod<2 else 8):
            n=12 if b.lod<2 else 8
            a=2*pi*i/n
            b.box((.38*cos(a),.38*sin(a),.51),(.13,.27 if n==12 else .35,1.02),
                  "Oak" if i%3 else "Oak Edge",.038,(0,0,a))
        b.beam((0,0,0),(0,0,1.03),.36,"Oak",sides=10)
        for z in (.20,.82):
            b.beam((0,0,z-.053),(0,0,z+.053),.443,"Iron",sides=12)
    else:
        for i in range(4 if b.lod<2 else 3):
            n=4 if b.lod<2 else 3
            b.box((0,0,(i+.5)*.92/n),(1.10,.90,.92/n-.012),"Oak" if i%2 else "Oak Edge",.044)
        for s in (-1,1):
            b.box((s*.43,0,.49),(.13,.98,.96),"Dark Wood",.023)
        b.beam((-.47,-.49,.15),(.47,-.49,.83),.057,"Oak Edge",sides=4)


def bridge(b,seed):
    r=random.Random(seed)
    bands=18 if b.lod==0 else 12 if b.lod==1 else 7
    for i in range(bands):
        y=-5.4+(i+.5)*10.8/bands
        z=.43*max(0,1-(y/5.4)**2)
        b.box((0,y,z),(3.06,10.8/bands+.035,.25),r.choice(("Oak","Oak Edge","Oak")),.065,(atan2(-.86*y/29.16,1),0,0))
    for side in (-1,1):
        x=side*1.40
        for y in (-4.82,-2.45,0,2.45,4.82):
            z=.43*max(0,1-(y/5.4)**2)
            b.beam((x,y,z-.28),(x,y,z+1.04),.118,"Dark Wood",.098)
            b.box((x,y,z+1.03),(.28,.29,.12),"Oak Edge",.043)
        for a,c in zip((-5.37,-2.5,0,2.5),(-2.5,0,2.5,5.37)):
            z=lambda y: 1+.43*max(0,1-(y/5.4)**2)
            b.beam((x,a,z(a)),(x,c,z(c)),.087,"Oak Edge",sides=5)
    for y in (-3.65,3.65):
        for x in (-1.09,1.09):
            b.beam((x,y,-1.8),(x,y,.22),.22,"Dark Wood")


def weapon(b,kind,at=(0,0,0),bone=None,rot=None):
    local=Build(b.lod)
    if kind in ("sword","dagger","runeblade"):
        short=kind=="dagger"
        myth=kind=="runeblade"
        blade=.43 if short else .78 if not myth else 1.06
        w=.067 if short else .102 if not myth else .213
        start=.20
        local.beam((0,0,0),(0,0,start),.056,"Leather",.043,8)
        for i in range(5 if b.lod<2 else 2):
            z=.021+i*.16/(5 if b.lod<2 else 2)
            local.beam((0,0,z),(0,0,z+.014),.058,"Oak Edge",.051,8)
        local.oval((0,0,.012),(.079,.066,.063),"Gold" if myth else "Iron")
        local.beam((-w*1.78,0,start+.010),(w*1.78,0,start+.010),.034,"Gold" if myth else "Iron",sides=8)
        edge=[(-w,start+.031),(-w*.91,start+blade*.68),(0,start+blade),(w*.91,start+blade*.68),(w,start+.031)]
        local.plate(list(reversed(edge)),0,.036,"Steel" if not myth else "Jade")
        inset=[(-w*.42,start+.049),(0,start+blade*.87),(w*.42,start+.049)]
        local.plate(list(reversed(inset)),-.024,.019,"Iron" if not myth else "Gold")
        if myth:
            for side in (-1,1):
                local.beam((side*.082,0,start),(side*.277,0,start+.145),.042,"Gold",.021,6)
            for z in (.39,.60,.79):
                local.plate([(0,z-.075),(.042,z),(0,z+.068),(-.042,z)],-.038,.015,"Jade")
    elif kind=="staff":
        local.beam((0,0,0),(0,0,1.57),.052,"Dark Wood",.063,7)
        for z in (.16,.76,1.29):
            local.beam((0,0,z),(0,0,z+.09),.068,"Gold",sides=8)
        for side in (-1,1):
            local.beam((0,0,1.35),(side*.19,0,1.62),.07,"Gold",.041,6)
            local.beam((side*.19,0,1.62),(side*.075,0,1.86),.041,"Gold",.021,6)
        local.oval((0,0,1.67),(.13,.11,.20),"Violet",sides=7)
    elif kind=="maul":
        local.beam((0,0,0),(0,0,1.04),.063,"Black Iron",.087,8)
        for z in (.10,.23,.64):
            local.beam((0,0,z),(0,0,z+.041),.074,"Oak Edge",.074,8)
        local.box((0,0,.94),(.53,.29,.35),"Basalt",.067)
        for side in (-1,1):
            local.box((side*.226,0,.94),(.13,.318,.338),"Red Iron",.036)
        for side in (-1,1):
            local.box((0,side*.16,.94),(.125,.046,.125),"Jade",.014,(0,.7854,0))
    else:
        local.beam((0,0,0),(0,0,1.06),.063,"Oak",.14,8)
        local.beam((0,0,.64),(0,0,1.15),.168,"Dark Wood",.211,7)
        for z in (.74,.98):
            local.beam((0,0,z),(0,0,z+.072),.198 if z<.8 else .22,"Iron",sides=8)
        for side in (-1,1):
            local.beam((side*.12,0,1.0),(side*.30,0,1.10),.064,"Bone",.013)
    from mathutils import Euler,Vector
    transform=Euler(rot or (0,0,0)).to_matrix()
    for mat in dict.fromkeys(local.tags):
        fs=[f for f,t in zip(local.faces,local.tags) if t==mat]
        used=sorted({i for f in fs for i in f})
        remap={old:i for i,old in enumerate(used)}
        b.mesh([tuple(Vector(at)+transform@Vector(local.verts[i])) for i in used],
               [tuple(remap[i] for i in f) for f in fs],mat,bone)
