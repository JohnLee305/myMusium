export const COLOR_FIELDS = [
 ['backWall','정면 벽','공간','#C4BFAD'],['frontWall','입구 벽','공간','#C4BFAD'],
 ['leftWall','왼쪽 벽','공간','#C4BFAD'],['rightWall','오른쪽 벽','공간','#C4BFAD'],
 ['floor','바닥','공간','#2E3B40'],['background','위쪽 배경','공간','#1F262B'],
 ['plinth','전시대 몸체','전시대','#9C9E91'],['cap','전시대 상판','전시대','#A35C24'],
 ['accent','벽 장식선','전시대','#A35C24'],
 ['plaqueBackground','명판 바탕','명판','#091612'],['plaqueBorder','명판 테두리·구분선','명판','#A3753B'],
 ['plaqueText','명판 제목·소개','명판','#F7F0D4'],['plaqueHint','명판 하단 안내','명판','#DBBD7D'],
 ['signBackground','안내판 바탕','방 안내판','#091814'],['signBorder','안내판 테두리·바닥 이동표시','방 안내판','#916129'],
 ['signText','안내판·박물관 제목 글자','방 안내판','#F0E8C9'],['signAccent','안내판 강조 글자','방 안내판','#C7A361'],
 ['signMount','안내판 뒤 받침','방 안내판','#090B0A'],
 ['detailBackground','설명창 바탕','상세 설명창','#132621'],['detailText','설명창 글자','상세 설명창','#E6E8DB'],
 ['detailAccent','설명창 장식선','상세 설명창','#B89452'],
 ['light','주 조명 색','조명','#FFF0D6'],['ambient','주변 조명 색','조명','#8C9199']
];
export const DEFAULT_THEME = Object.fromEntries(COLOR_FIELDS.map(([key,,,value])=>[key,value]));
const palette = (name,colors) => ({name,colors:{...DEFAULT_THEME,...colors}});
export const PRESETS = {
 classic: palette('기본 박물관',{}),
 bright: palette('밝은 갤러리',{backWall:'#E8E8E4',frontWall:'#E8E8E4',leftWall:'#E8E8E4',rightWall:'#E8E8E4',floor:'#B1B7B5',background:'#CFD6DA',plinth:'#E0E1DD',cap:'#555F61',accent:'#555F61',plaqueBackground:'#F5F4ED',plaqueBorder:'#64706E',plaqueText:'#203430',plaqueHint:'#40564E',signBackground:'#F5F4ED',signBorder:'#64706E',signText:'#203430',signAccent:'#40564E',detailBackground:'#F5F4ED',detailText:'#203430',detailAccent:'#64706E',light:'#FFFFFF',ambient:'#AAB2BA'}),
 night: palette('밤의 전시실',{backWall:'#202D40',frontWall:'#202D40',leftWall:'#293548',rightWall:'#293548',floor:'#131C2A',background:'#0F1725',plinth:'#384352',cap:'#9B8252',accent:'#9B8252',plaqueBackground:'#152033',plaqueBorder:'#9B8252',plaqueText:'#F1EAD7',plaqueHint:'#D7B97C',signBackground:'#152033',signBorder:'#9B8252',signText:'#F1EAD7',signAccent:'#D7B97C',detailBackground:'#152033',detailText:'#F1EAD7',detailAccent:'#9B8252',light:'#E7EEFF',ambient:'#858B9B'}),
 warm: palette('따뜻한 서재',{backWall:'#B99C7B',frontWall:'#B99C7B',leftWall:'#C7AF90',rightWall:'#C7AF90',floor:'#51413B',background:'#372C29',plinth:'#937B64',cap:'#52362B',accent:'#735036',plaqueBackground:'#36261F',plaqueBorder:'#B68F57',plaqueText:'#F8EACF',plaqueHint:'#E2C68F',signBackground:'#36261F',signBorder:'#B68F57',signText:'#F8EACF',signAccent:'#E2C68F',detailBackground:'#36261F',detailText:'#F8EACF',detailAccent:'#B68F57',light:'#FFE6C5',ambient:'#A39788'})
};
export function validateTheme(theme) {
 if (!theme || typeof theme!=='object' || Array.isArray(theme)) throw Object.assign(new Error('테마는 색상 목록이어야 합니다.'),{status:400});
 for (const [key,value] of Object.entries(theme)) {
  if (!Object.hasOwn(DEFAULT_THEME,key) || typeof value!=='string' || !/^#[0-9a-fA-F]{6}$/.test(value))
   throw Object.assign(new Error('지원되는 항목에 #RRGGBB 형식의 색상을 입력해 주세요.'),{status:400});
 }
 return Object.fromEntries(Object.entries(theme).map(([key,value])=>[key,value.toUpperCase()]));
}
export function effectiveTheme(theme) { return {...DEFAULT_THEME,...(theme||{})}; }
