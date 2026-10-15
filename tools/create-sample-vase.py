"""Original procedural sample. No external models or textures required."""
import math
from pathlib import Path
import bpy

root = Path(__file__).resolve().parent.parent
output = root / 'museum-archive/artifacts/sample-vase/models/pc.glb'
output.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
# Outside rises from the foot to the lip; inside returns to a closed base.
profile = [(0,0),(.09,0),(.095,.025),(.14,.075),(.17,.15),(.165,.23),
           (.13,.31),(.085,.365),(.075,.405),(.09,.425),(.09,.445),
           (.075,.445),(.062,.42),(.062,.395),(.072,.36),(.115,.30),
           (.148,.225),(.152,.15),(.12,.085),(.075,.035),(0,.035)]
segments = 64
verts = [(r*math.cos(i*math.tau/segments),r*math.sin(i*math.tau/segments),z)
         for r,z in profile for i in range(segments)]
faces = []
for row in range(len(profile)-1):
    for i in range(segments):
        j = (i+1)%segments
        faces.append((row*segments+i,row*segments+j,(row+1)*segments+j,(row+1)*segments+i))
mesh = bpy.data.meshes.new('VaseMesh')
mesh.from_pydata(verts, [], faces)
mesh.update()
obj = bpy.data.objects.new('Sample ceramic vase', mesh)
bpy.context.collection.objects.link(obj)
bpy.context.view_layer.objects.active = obj
obj.select_set(True)
for polygon in mesh.polygons:
    polygon.use_smooth = True
material = bpy.data.materials.new('Teal ceramic')
material.use_nodes = True
bsdf = material.node_tree.nodes.get('Principled BSDF')
bsdf.inputs['Base Color'].default_value = (0.025,0.28,0.24,1)
bsdf.inputs['Roughness'].default_value = 0.24
obj.data.materials.append(material)
assert bpy.ops.export_scene.gltf(filepath=str(output), export_format='GLB') == {'FINISHED'}
print('SAMPLE_VASE_PASS: ' + str(output))
