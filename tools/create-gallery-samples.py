"""Original sample models, generated locally without third-party assets."""
import json
from pathlib import Path
import bpy

root = Path(__file__).resolve().parent.parent / 'museum-archive'

def material(name, color, metallic=0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes['Principled BSDF']
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Roughness'].default_value = .35
    return mat

def part(name, location, size, mat):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    bevel = obj.modifiers.new('Soft edges', 'BEVEL')
    bevel.width = .008
    bevel.segments = 3
    return obj

for kind in ('books', 'sculpture'):
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    if kind == 'books':
        paper = material('Ivory paper', (.8, .73, .56))
        for i, color in enumerate(((.4,.08,.06), (.04,.14,.32), (.24,.28,.06))):
            cover = material('Book cover ' + str(i), color)
            z = i * .09
            for dz in (.008, .076):
                part('Cover', (0,0,z+dz), (.43,.30,.016), cover)
            part('Pages', (.006,0,z+.042), (.40,.275,.052), paper)
            part('Spine', (-.207,0,z+.042), (.016,.30,.065), cover)
        title = '겹겹의 이야기 · 샘플'
        description = '책 세 권을 쌓아 만든 개발용 모형입니다.\n가족의 책과 기록을 전시할 자리를 상상해 보세요.'
    else:
        brass = material('Sculpture brass', (.55,.25,.055), .7)
        stone = material('Dark stone', (.10,.12,.14))
        part('Base', (0,0,.035), (.36,.26,.07), stone)
        bpy.ops.mesh.primitive_torus_add(major_radius=.17, minor_radius=.035, location=(0,0,.28), rotation=(1.5707963,0,0))
        bpy.context.object.name = 'Memory ring'
        bpy.context.object.data.materials.append(brass)
        for polygon in bpy.context.object.data.polygons:
            polygon.use_smooth = True
        title = '이어지는 기억 · 샘플'
        description = '둥근 고리와 받침으로 만든 개발용 조형물입니다.\n외부 에셋 없이 직접 생성했습니다.'
    ident = 'sample-' + kind
    folder = root / 'artifacts' / ident
    (folder / 'models').mkdir(parents=True, exist_ok=True)
    assert bpy.ops.export_scene.gltf(filepath=str(folder / 'models/pc.glb'), export_format='GLB') == {'FINISHED'}
    data = dict(schemaVersion='1.0', id=ident, title=title, description=description, model=f'artifacts/{ident}/models/pc.glb', scale=1.0)
    (folder / 'artifact.json').write_text(json.dumps(data, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    (folder / 'README.md').write_text('# 개발용 샘플\n\ntools/create-gallery-samples.py로 직접 생성. 외부 모델·텍스처 사용 없음. 실제 소장품 아님. 단위는 미터.\n', encoding='utf-8')
    print('GALLERY_SAMPLE_PASS: ' + ident)
