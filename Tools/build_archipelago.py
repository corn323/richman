import bpy, math, json, os
from pathlib import Path
from mathutils import Vector, Matrix

PROJECT_ROOT = Path(__file__).resolve().parents[1]
OUT = str(PROJECT_ROOT / 'GodotProject')
ASSET = os.path.join(OUT, 'Assets')
DATA = os.path.join(OUT, 'Data')
os.makedirs(ASSET, exist_ok=True); os.makedirs(DATA, exist_ok=True)

# Preserve all existing objects and add the map to its own categorized collections.
scene = bpy.context.scene
def remove_map_object(obj, owner_collection):
    other_scene_users=[user_scene for user_scene in obj.users_scene if user_scene != scene]
    if other_scene_users:
        owner_collection.objects.unlink(obj)
        return
    object_type=obj.type
    data=obj.data
    bpy.data.objects.remove(obj, do_unlink=True)
    if data is not None and data.users == 0:
        if object_type == 'MESH': bpy.data.meshes.remove(data)
        elif object_type in ('CURVE','FONT'): bpy.data.curves.remove(data)
        elif object_type == 'LIGHT': bpy.data.lights.remove(data)
        elif object_type == 'CAMERA': bpy.data.cameras.remove(data)
for old_collection in list(scene.collection.children):
    if old_collection.name.startswith(('Terrain', 'River and Sea', 'MainRoute', 'Landmarks', 'Buildings', 'Decorations', 'ChanceDecks')):
        for old_object in list(old_collection.objects): remove_map_object(old_object, old_collection)
for old_object in list(scene.objects):
    if old_object.name.startswith(('Large softbox', 'Ocean fill', 'Isometric map camera')):
        if old_object.name in scene.collection.objects: remove_map_object(old_object, scene.collection)
with open(os.path.join(DATA, 'archipelago_tiles.json'), encoding='utf-8') as source:
    CORE_MAP = json.load(source)
CORE_TILES = CORE_MAP['tiles']
CORE_MAIN_COUNT = CORE_MAP['main_route_tile_count']
scene.render.engine = 'BLENDER_EEVEE'
scene.render.resolution_x = 1440; scene.render.resolution_y = 1100; scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'; scene.render.film_transparent = False

scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'Medium High Contrast'

def collection(name):
    for existing in scene.collection.children:
        if existing.name == name or existing.name.startswith(name + '.'):
            return existing
    c=bpy.data.collections.new(name); scene.collection.children.link(c); return c
terrain=collection('Terrain'); water=collection('River and Sea'); route=collection('MainRoute - data-driven stable tile sockets'); landmarks=collection('Landmarks'); buildings=collection('Buildings'); deco=collection('Decorations'); decks=collection('ChanceDecks')
MAP_COLLECTIONS={terrain,water,route,landmarks,buildings,deco,decks}
def mat(name, color, rough=0.78, metal=0):
    m=bpy.data.materials.get(name) or bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED'); p.inputs['Base Color'].default_value=(*color,1); p.inputs['Roughness'].default_value=rough; p.inputs['Metallic'].default_value=metal
    return m
grass=mat('Island grass',(.26,.58,.32)); grass2=mat('Highland meadow',(.49,.71,.34)); cliff=mat('Warm limestone',(.67,.51,.34)); sand=mat('Shore sand',(.92,.75,.43)); sea=mat('Deep turquoise water',(.035,.35,.52),.3); river=mat('Bright canal',(.08,.58,.72),.25); road=mat('Cream promenade',(.91,.80,.60)); roadedge=mat('Terracotta road edge',(.64,.31,.19)); white=mat('Porcelain',(.94,.91,.82)); roofred=mat('Coral roofs',(.86,.25,.17)); roofblue=mat('Teal roofs',(.12,.42,.49)); roofgold=mat('Golden roofs',(.94,.60,.17)); glass=mat('Blue glass',(.22,.60,.76),.22); green=mat('Canopy',(.12,.38,.20)); green2=mat('Canopy light',(.34,.61,.23)); trunk=mat('Tree bark',(.30,.18,.11)); dark=mat('Ink navy',(.07,.13,.21)); pink=mat('Chance lavender',(.63,.30,.79)); fate=mat('Destiny blue',(.16,.40,.78)); red=mat('Airport orange',(.95,.34,.12)); gold=mat('Start gold',(1,.73,.12),.45,.15); white2=mat('White',(.98,.97,.88)); yellow=mat('Window glow',(1,.78,.29),.42)
def mesh_obj(name, verts, faces, material, col, bevel=0):
    me=bpy.data.meshes.new(name+'Mesh'); me.from_pydata(verts,[],faces); me.materials.append(material); ob=bpy.data.objects.new(name,me); col.objects.link(ob)
    for linked in list(ob.users_collection):
        if linked not in MAP_COLLECTIONS: linked.objects.unlink(ob)
    if bevel:
        mod=ob.modifiers.new('Soft toy edges','BEVEL'); mod.width=bevel; mod.segments=2
        ob.modifiers.new('Weighted corner normals','WEIGHTED_NORMAL')
    return ob
def cube(name, loc, scale, material, col, bevel=.08):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc); ob=bpy.context.object; ob.name=name; ob.dimensions=scale; bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    # move from default collection to categorized one
    for c in list(ob.users_collection): c.objects.unlink(ob)
    col.objects.link(ob)
    for linked in list(ob.users_collection):
        if linked not in MAP_COLLECTIONS: linked.objects.unlink(ob)
    ob.data.materials.append(material)
    if bevel:
        m=ob.modifiers.new('Rounded handcrafted edges','BEVEL'); m.width=bevel; m.segments=3; ob.modifiers.new('Corner normals','WEIGHTED_NORMAL')
    return ob
def uv(name, loc, scale, material, col, seg=12, rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, location=loc); ob=bpy.context.object; ob.name=name; ob.scale=scale
    for c in list(ob.users_collection): c.objects.unlink(ob)
    col.objects.link(ob)
    for linked in list(ob.users_collection):
        if linked not in MAP_COLLECTIONS: linked.objects.unlink(ob)
    ob.data.materials.append(material); bpy.ops.object.shade_smooth(); return ob
def cyl(name, loc, radius, depth, material, col, vertices=12, rtop=None):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius, radius2=radius if rtop is None else rtop, depth=depth, location=loc); ob=bpy.context.object; ob.name=name
    for c in list(ob.users_collection): c.objects.unlink(ob)
    col.objects.link(ob)
    for linked in list(ob.users_collection):
        if linked not in MAP_COLLECTIONS: linked.objects.unlink(ob)
    ob.data.materials.append(material); ob['y_up_cylinder']=True; return ob
def label(name, body, loc, size, material, col):
    cu=bpy.data.curves.new(name,'FONT'); cu.body=body; cu.align_x='CENTER'; cu.align_y='CENTER'; cu.size=size; cu.extrude=.005; ob=bpy.data.objects.new(name,cu); col.objects.link(ob)
    for linked in list(ob.users_collection):
        if linked not in MAP_COLLECTIONS: linked.objects.unlink(ob)
    ob.location=loc; cu.materials.append(material); return ob
def tube(name, points, radius, material, col, cyclic=False):
    cu=bpy.data.curves.new(name,'CURVE'); cu.dimensions='3D'; cu.resolution_u=2; cu.bevel_depth=radius; cu.bevel_resolution=3
    sp=cu.splines.new('POLY'); sp.points.add(len(points)-1)
    for p,co in zip(sp.points,points): p.co=(co[0],co[1],co[2],1)
    sp.use_cyclic_u=cyclic; ob=bpy.data.objects.new(name,cu); col.objects.link(ob)
    for linked in list(ob.users_collection):
        if linked not in MAP_COLLECTIONS: linked.objects.unlink(ob)
    cu.materials.append(material); return ob

# Positions and route ids come from the shared Core export.
n=CORE_MAIN_COUNT
coords=[(t['position']['x'], t['position']['z']) for t in CORE_TILES[:n]]
heights=[t['position']['y'] for t in CORE_TILES[:n]]
tiles=[]
for i,tile in enumerate(CORE_TILES):
    x=tile['position']['x']; z=tile['position']['z']; y=tile['position']['y']+.16
    t=tile['type'].lower(); name=tile['display_name']; tile=dict(tile); tiles.append(tile)
    plate_mat=gold if t=='start' else red if t=='airport' else dark if t=='prison' else white if t=='hospital' else cliff if t=='court' else pink if t=='chance' else fate if t=='destiny' else mat('District_'+str(i),((.30+.55*(i%3)/2),(.44+.42*((i+1)%4)/3),(.28+.52*((i+2)%5)/4)))
    # path module with border and bright readable top
    tileob=cube('TileSocket_%02d_%s'%(i,t),(x,y-.12,z),(1.40,.28,1.17),roadedge,route,.11); tileob['tile_id']=tile['id']; tileob['position_index']=i; tileob['tile_type']=t
    top=cube('TileTop_%02d'%i,(x,y+.035,z),(1.32,.12,1.09),plate_mat,route,.08); top['tile_id']=tile['id']
    # colored district ribbon at landward edge, retains clear pawn centre
    cube('DistrictBand_%02d'%i,(x,y+.105,z+.42),(1.12,.07,.12),gold if t=='start' else roofblue if t=='property' else plate_mat,route,.025)
    label('TileLabel_%02d'%i,name,(x,y+.28,z-.36),.18,dark if t in ('start','hospital','chance','destiny','property') else white2,route)
    if t.lower() in ('start','airport','hospital','prison','court','chance','destiny'):
        icon={'start':'START','airport':'✈','hospital':'+','prison':'JAIL','court':'§','chance':'?','destiny':'✦'}.get(t.lower(),'')
        if icon:
            label('Icon_%02d'%i,icon,(x,y+.34,z+.12),.30,white2 if t in ('airport','prison','destiny') else dark,route)

# Stylized sculpted coastline: layered stepped land mass with rocky vertical faces.
def island(cx,cz,sx,sz,top,seed,material=grass):
    seg=32; verts=[]
    for ring,(scale,y) in enumerate(((1.08,.05),(.99,.28),(.86,top))):
        for j in range(seg):
            a=2*math.pi*j/seg; wobble=1+.055*math.sin(a*5+seed)+.035*math.sin(a*9+seed*.7)
            verts.append((cx+sx*scale*wobble*math.cos(a),y,cz+sz*scale*wobble*math.sin(a)))
    faces=[]
    for r in range(2):
        for j in range(seg): faces.append((r*seg+j,r*seg+(j+1)%seg,(r+1)*seg+(j+1)%seg,(r+1)*seg+j))
    faces.append(tuple(range(2*seg,3*seg)))
    ob=mesh_obj('IslandTerrain_%02d'%seed,verts,faces,material,terrain); ob['region']='raised island'; ob.data.materials.append(cliff)
    # cliff coloring on first two face bands
    for p in ob.data.polygons:
        if p.index<seg: p.material_index=1
    return ob
island(0,0,13.8,10.2,.88,3,grass)
# clear flowing blue river loops through open center, shallow sand banks
for idx,(cx,cz,sx,sz) in enumerate(((-.2,.2,4.8,3.4),(0,0,6.6,5.0))):
    pts=[]
    for j in range(96):
        a=2*math.pi*j/96; pts.append((cx+sx*math.cos(a)+.2*math.sin(3*a),.20,cz+sz*math.sin(a)))
    tube('CanalBank_%d'%idx,pts,.27,sand,water,True)
    tube('CanalWater_%d'%idx,[(x,.23,z) for x,_,z in pts],.18,river,water,True)
# Two arched pedestrian bridges cross the canal rings and keep the inner garden reachable.
for side in (-1,1):
    bx=side*5.65
    cube('CanalBridge_%s stone deck'%('West' if side<0 else 'East'),(bx,.55,0),(2.35,.22,.92),sand,landmarks,.12)
    for zoff in (-.43,.43):
        tube('CanalBridge_%s handrail'%('West' if side<0 else 'East'),[(bx-1.05,.76,zoff),(bx-.55,1.02,zoff),(bx,1.10,zoff),(bx+.55,1.02,zoff),(bx+1.05,.76,zoff)],.055,white,landmarks)
    for xoff in (-.86,.86):
        cyl('CanalBridge %s pier'%('West' if side<0 else 'East'),(bx+xoff,.35,0),.12,.56,cliff,landmarks,10)
# central garden island and plaza
island(0,0,2.7,2.15,1.20,41,grass2)
cyl('Central Fountain Basin',(0,1.28,0),1.02,.18,glass,landmarks,32)
cyl('Fountain Inner',(0,1.51,0),.59,.28,white,landmarks,24)
uv('Fountain Globe',(0,1.89,0),(.30,.42,.30),glass,landmarks)
for a in range(8):
    ang=a*math.tau/8; x=1.9*math.cos(ang); z=1.55*math.sin(ang)
    cyl('Garden Flower Stem',(x,1.58,z),.035,.42,green,landmarks,8)
    uv('Flower',(x,1.8,z),(.18,.13,.18),[roofred,pink,gold,white2][a%4],landmarks)

# Road: build every graph edge, including each branch and merge, from Core positions.
tile_by_id={tile['id']:tile for tile in CORE_TILES}
for tile in CORE_TILES:
    source=tile['position']
    for next_id in tile['next_tiles']:
        target=tile_by_id[next_id]['position']; dx=target['x']-source['x']; dz=target['z']-source['z']; d=math.hypot(dx,dz); angle=-math.atan2(dz,dx)
        mid=( (source['x']+target['x'])/2, (source['y']+target['y'])/2-.16, (source['z']+target['z'])/2 )
        ob=cube('Promenade_%s_to_%s'%(tile['id'],next_id),mid,(d+.20,.22,.62),road,route,.12); ob.rotation_euler[1]=angle
        edge=cube('Promenade_Rim_%s_to_%s'%(tile['id'],next_id),(mid[0],mid[1]-.12,mid[2]),(d+.27,.12,.78),roadedge,route,.09); edge.rotation_euler[1]=angle

# Geographical city zones, sculpted landmarks, tree groves.
def tree(x,z,s=1):
    base=.72+.25*math.sin(x*.7)+.15*math.cos(z*.8)
    cyl('Tree trunk',(x,base+.34*s,z),.12*s,.68*s,trunk,deco,9)
    uv('Tree crown lower',(x,base+.88*s,z),(.52*s,.43*s,.52*s),green,deco,12,8)
    uv('Tree crown sunlit',(x+.10*s,base+1.10*s,z-.08*s),(.40*s,.36*s,.40*s),green2,deco,12,8)
def house(x,z,s=1,color=roofred,tower=False):
    base=.82+.20*math.sin(x)+.08*math.cos(z)
    cube('Pastel townhouse',(x,base+.42*s,z),(.95*s,.84*s,.82*s),white,buildings,.09)
    # gabled roof mesh
    verts=[(x-.58*s,base+.82*s,z-.5*s),(x+.58*s,base+.82*s,z-.5*s),(x,base+1.48*s,z-.5*s),(x-.58*s,base+.82*s,z+.5*s),(x+.58*s,base+.82*s,z+.5*s),(x,base+1.48*s,z+.5*s)]
    mesh_obj('Terracotta gable',verts,[(0,1,2),(3,5,4),(0,3,4,1),(0,2,5,3),(1,4,5,2)],color,buildings,.025)
    cube('Townhouse door',(x,base+.25*s,z-.421*s),(.19*s,.48*s,.035),roofblue,buildings,.025)
    for wx in (-.28,.28): cube('Glazed window',(x+wx*s,base+.54*s,z-.425*s),(.16*s,.21*s,.035),yellow,buildings,.018)
    if tower:
        cyl('Clock tower shaft',(x,base+1.55*s,z),.24*s,1.5*s,white,landmarks,12)
        cyl('Clock copper cap',(x,base+2.35*s,z),.38*s,.35*s,roofgold,landmarks,10,0)
        uv('Clock face',(x,base+1.88*s,z-.25*s),(.18*s,.18*s,.05),white2,landmarks)
def skyscraper(x,z,h,color=glass):
    base=.84+.12*math.sin(z)
    cube('Skyline tower',(x,base+h/2,z),(1.08,h,.88),color,buildings,.1)
    for y in [base+h*.22,base+h*.42,base+h*.62,base+h*.82]:
        cube('Tower illuminated window strip',(x, y,z-.45),(0.78,.075,.035),yellow,buildings,.015)
    cyl('Tower crown',(x,base+h+.17,z),.66,.34,roofgold,buildings,8,.08)
def tree_line(cx,cz,count,rx_,rz_,phase=0):
    for j in range(count):
        a=phase+math.tau*j/count; tree(cx+rx_*math.cos(a),cz+rz_*math.sin(a),.62+.32*((j%3)/2))
# districts, kept well inside the outer route.
for row in range(4):
    for col in range(5):
        x=-8+col*4; z=-5.6+row*3.7
        # preserve central river and plaza channel; clustered house lots around it
        if abs(x)<3 and abs(z)<2.3: continue
        if (row+col)%3==0: house(x,z,.72+(col%2)*.12,[roofred,roofblue,roofgold][(row+col)%3],tower=(row==3 and col==1))
        elif (row+col)%3==1: skyscraper(x,z,2.3+(row*col)%3*.55,glass if row%2 else white)
        if (row*5+col)%2==0: tree(x+.9,z+.8,.82)
tree_line(-5,3,12,2.1,1.35,.1); tree_line(6,-3,11,2.4,1.3,.25)
# recognizable themed landmarks
house(-8.2,-4.7,1.15,roofred,True) # London clock tower
skyscraper(8.0,4.7,4.3,glass); cyl('Tokyo tower spire',(8.0,6.1,4.7),.3,1.2,roofred,landmarks,8,.03)
# Colosseum-inspired landmark
cx,cz=6.9,-5.7
cyl('Roman arena lower',(cx,1.0,cz),1.55,.48,sand,landmarks,32)
cyl('Roman arena upper',(cx,1.42,cz),1.26,.38,roofgold,landmarks,32)
cyl('Roman arena inner court',(cx,1.63,cz),.77,.08,roadedge,landmarks,32)
for j in range(12):
    a=math.tau*j/12; x=cx+1.39*math.cos(a); z=cz+1.39*math.sin(a)
    cube('Arena arch pier',(x,1.48,z),(.18,.62,.18),white,landmarks,.04)
# Hospital and prison / courthouse with silhouette-specific forms.
def route_position(tile_type):
    tile=next((t for t in CORE_TILES if t['type']==tile_type),None)
    if tile is None: raise ValueError('Required Core route tile is missing: '+tile_type)
    p=tile['position']; return p['x'],p['z']
hx,hz=route_position('Hospital'); cube('Hospital main block',(hx,2.0,hz), (1.9,2.35,1.35),white,landmarks,.13)
cube('Hospital red cross horizontal',(hx,3.5,hz-.72),(1.05,.23,.09),roofred,landmarks,.025); cube('Hospital red cross vertical',(hx,3.5,hz-.72),(.23,1.05,.09),roofred,landmarks,.025)
for xoff in (-.58,0,.58): cube('Hospital window',(hx+xoff,2.6,hz-.70),(.22,.28,.05),glass,landmarks,.025)
px,pz=route_position('Prison'); cube('Prison keep',(px,1.75,pz),(1.7,1.6,1.25),dark,landmarks,.08)
for xoff in (-.42,-.14,.14,.42): cube('Prison bar',(px+xoff,1.7,pz-.67),(.045,.72,.04),white2,landmarks,.01)
cx,cz=route_position('Court'); cube('Court marble hall',(cx,1.42,cz),(2.0,1.48,1.4),white,landmarks,.08)
for xoff in (-.7,-.24,.24,.7): cyl('Courthouse column',(cx+xoff,1.7,cz-.73),.12,1.15,white,landmarks,12)
cyl('Courthouse pediment',(cx,2.36,cz-.72),1.12,.5,roofgold,landmarks,3,0)
# airports and aircraft are on route side but offset behind the tile
for idx in [t['position_index'] for t in CORE_TILES[:n] if t['type']=='Airport']:
    x,z=coords[idx]; cube('Airfield apron',(x,1.0,z+1.65),(3.2,.12,2.0),dark,landmarks,.12)
    label('Airport runway marking','✈',(x,1.09,z+1.65),.76,white2,landmarks)
    # stylized aircraft: fuselage, wings, tail, twin engines
    cube('Airliner fuselage',(x,1.55,z+2.0),(.42,.48,2.5),white2,landmarks,.18)
    cube('Airliner swept wings',(x,1.49,z+2.03),(2.55,.20,.62),white,landmarks,.14)
    cube('Airliner tailplane',(x,1.62,z+3.02),(1.0,.15,.32),white,landmarks,.08)
    cube('Airliner coral tail',(x,1.83,z+3.06),(.42,.85,.14),roofred,landmarks,.06)
# START flag + checker board; model flag is separate from tile top.
sx,sz=coords[0]
cyl('Start flagpole',(sx-.62,1.85,sz+.17),.035,2.8,white2,landmarks,8)
mesh_obj('Start golden flag',[(sx-.60,2.88,sz+.17),(sx+.15,2.75,sz+.17),(sx-.60,2.48,sz+.17)],[(0,1,2)],gold,landmarks)
for j in range(5):
    for k in range(4): cube('Start checker',(sx-.36+j*.18,1.18,sz-.17+k*.15),(.18,.035,.15),dark if (j+k)%2==0 else white2,landmarks,.005)
# two physically stacked deck platforms and visible repeated card slabs.
for xx,title,material in ((-.98,'CHANCE',pink),(.98,'DESTINY',fate)):
    cube(title+' Deck Pedestal',(xx,1.36,0),(1.55,.28,1.85),dark,decks,.15)
    for j in range(5): cube(title+' Physical Card %d'%j,(xx,1.56+j*.055,0),(1.20,.09,1.42),material if j==4 else white2,decks,.09)
    cube(title+' Deck Top',(xx,1.88,0),(1.12,.08,1.32),material,decks,.08)
    label(title+' Deck Label',title,(xx,1.95,.15),.23,white2,decks)
# The JSON is exported by Richman.Core; never reconstruct or overwrite route data here.

# sea disc beneath islands, multiple shoals/rock reefs and tiny coastal beacons
cyl('Ocean plinth',(0,-.55,0),17.7,1.0,sea,water,96)
for j in range(22):
    a=math.tau*j/22; x=15.6*math.cos(a); z=12.7*math.sin(a)
    cyl('Coastal rock',(x,-.05,z),.62+.15*(j%3),.65,cliff,water,8)
for x,z in ((-11,-7),(11,6),(-10,7),(10,-8)):
    cyl('Harbor beacon',(x,.9,z),.20,.8,white2,landmarks,10); cyl('Beacon roof',(x,1.38,z),.28,.22,roofred,landmarks,8,0)
# scene lighting/camera, isometric elevated overview
world=bpy.data.worlds.new('Archipelago World'); world.use_nodes=True; next(n for n in world.node_tree.nodes if n.type=='BACKGROUND').inputs['Color'].default_value=(.025,.13,.23,1); next(n for n in world.node_tree.nodes if n.type=='BACKGROUND').inputs['Strength'].default_value=.45
ld=bpy.data.lights.new('Large softbox','AREA'); lo=bpy.data.objects.new('Large softbox',ld); scene.collection.objects.link(lo); lo.location=(-9,-10,22); ld.energy=4200; ld.shape='DISK'; ld.size=14
ld2=bpy.data.lights.new('Ocean fill','AREA'); lo2=bpy.data.objects.new('Ocean fill',ld2); scene.collection.objects.link(lo2); lo2.location=(12,8,12); ld2.energy=2300; ld2.size=12
camd=bpy.data.cameras.new('Isometric map camera'); cam=bpy.data.objects.new('Isometric map camera',camd); scene.collection.objects.link(cam); scene.camera=cam; cam.location=(23,-29,24); target=Vector((0,0,0)); cam.rotation_euler=(target-Vector(cam.location)).to_track_quat('-Z','Y').to_euler(); camd.type='ORTHO'; camd.ortho_scale=37
# Convert the Y-up modeling layout into Blender Z-up; compensate default Z-axis primitives.
up=Matrix.Rotation(math.pi/2,4,'X'); local_up=Matrix.Rotation(-math.pi/2,4,'X')
map_objects=[o for c in MAP_COLLECTIONS for o in c.objects]
for ob in map_objects:
    if ob.type=='FONT': ob.matrix_world=up @ ob.matrix_world @ local_up
    elif ob.get('y_up_cylinder'): ob.matrix_world=up @ ob.matrix_world @ local_up
    else: ob.matrix_world=up @ ob.matrix_world
# Save source and export only the map objects.
bpy.ops.object.select_all(action='DESELECT')
blend=os.path.join(ASSET,'archipelago_map.blend'); glb=os.path.join(ASSET,'archipelago_map.glb')
bpy.ops.wm.save_as_mainfile(filepath=blend)
for ob in bpy.data.objects: ob.select_set(False)
export_objects=[o for o in map_objects if not o.name.startswith(('TileSocket_', 'TileTop_', 'DistrictBand_', 'TileLabel_', 'Icon_'))]
for ob in export_objects: ob.select_set(True)
bpy.context.view_layer.objects.active=next(o for o in export_objects if o.name.startswith('Ocean plinth'))
bpy.ops.export_scene.gltf(filepath=glb,export_format='GLB',use_selection=True,export_apply=True,export_yup=True,export_materials='EXPORT',export_cameras=False,export_lights=False,use_active_scene=True)
print('SAVED',blend,os.path.getsize(blend),'GLB',glb,os.path.getsize(glb),'source tiles',len(tiles),'main route',len([x for x in tiles if x.get('is_main_route')]),'branch nodes',len([x for x in tiles if not x.get('is_main_route')]),'exported tile surfaces',0)




