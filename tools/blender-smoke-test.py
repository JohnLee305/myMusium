"""Blender --background --factory-startup --python-exit-code 1 --python this-file."""
import json
import struct
from pathlib import Path
import bpy

project_root = Path(__file__).resolve().parent.parent
output_dir = project_root / 'samples' / 'environment-check'
output_dir.mkdir(parents=True, exist_ok=True)
output_file = output_dir / 'sample-cube.glb'
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.mesh.primitive_cube_add(size=0.1, location=(0, 0, 0))
bpy.context.object.name = 'EnvironmentCheckCube_10cm'
assert bpy.ops.export_scene.gltf(filepath=str(output_file), export_format='GLB') == {'FINISHED'}
data = output_file.read_bytes()
magic, version, length = struct.unpack_from('<4sII', data)
assert magic == b'glTF' and version == 2 and length == len(data)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
assert bpy.ops.import_scene.gltf(filepath=str(output_file)) == {'FINISHED'}
meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
assert len(meshes) == 1
assert all(abs(float(axis) - 0.1) < 1e-5 for axis in meshes[0].dimensions)
print(json.dumps({'result': 'PASS', 'blender': bpy.app.version_string,
                  'glb': str(output_file), 'bytes': len(data),
                  'meshCount': len(meshes), 'dimensionsMeters': list(meshes[0].dimensions)}))
