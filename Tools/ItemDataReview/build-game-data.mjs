import fs from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';
import { Workbook } from '@oai/artifact-tool';

const root = 'C:/Users/admin/Desktop/IndividualProject/3DExtractionShooter';
const sourceRoot = 'C:/Users/admin/Desktop/에셋';
async function readRows(file, name) {
  const wb = await Workbook.fromCSV((await fs.readFile(file, 'utf8')).replace(/^\uFEFF/, ''), { sheetName: name });
  const [headers, ...rows] = wb.worksheets.getItem(name).getUsedRange().values;
  return rows.filter(r => r.some(v => v !== '' && v !== null)).map(row => Object.fromEntries(headers.map((h, i) => [h, row[i] ?? ''])));
}
const sources = {};
for (const name of ['Weapons', 'Ammo', 'Medicine', 'Armor', 'Helmet']) sources[name] = await readRows(`${sourceRoot}/${name}/${name}.csv`, name);
const weaponFile = `${root}/Assets/GameData/Weapons/weapons.csv`;
const oldWeapons = await readRows(weaponFile, 'ExistingWeapons');
// 기존 ID와 주소, 직접 조정한 반동·탄퍼짐·세 단계 사거리는 그대로 이어받는다.
const assignments = [{name:'AK-47', id:1001, base:1001}, {name:'Glick', id:1002, base:1002}, {name:'MP-155', id:1003, base:1003}, {name:'MF', id:1004, base:1001}];
const weapons = assignments.map(({name, id, base}) => {
  const raw = sources.Weapons.find(r => r.Name === name);
  const previous = oldWeapons.find(r => Number(r.weaponId) === id) ?? oldWeapons.find(r => Number(r.weaponId) === base);
  assert(raw && previous);
  const row = { ...previous, weaponId:id, displayName:name, maxStackCount:1, unitWeight:Number(raw.Weight), iconAddress:previous.iconAddress ?? '',
    magazineCapacity:Number(raw.MagazineCapacity), reloadDuration:Number(raw.ReloadTime), fireRate:Number(raw.FireRate), baseDamage:Number(raw.Damage),
    projectileSpeed:Number(raw.BulletSpeed), shotNoiseRadius:Number(raw.SoundRange), bulletsPerShot:Number(raw.BulletsPerShot), singleShotAngle:Number(raw.SingleShotAngle),
    reloadMode:raw.WeaponClass === 'Shotgun' ? 'SingleRound' : 'Magazine', adsDuration:Number(raw.ADSTime),
    movementSpeedMultiplier:Number(raw.MovementSpeedCoefficient), adsMovementSpeedMultiplier:Number(raw.ADSMovementCoefficient) };
  const ratio = Number(previous.aimSpreadMultiplier);
  row.adsBaseSpreadAngle = Number(previous.baseSpreadAngle) * ratio;
  row.adsMaxSpreadAngle = Number(previous.maxSpreadAngle) * ratio;
  row.adsSpreadIncreasePerShot = Number(previous.spreadIncreasePerShot) * ratio;
  row.adsSpreadRecoverySpeed = Number(previous.spreadRecoverySpeed) * ratio;
  if (id === 1004 && !oldWeapons.some(r => Number(r.weaponId) === id)) {
    // 새 무기의 외형과 음원은 존재하지 않는 주소를 만들어 넣지 않는다.
    row.weaponViewAddress = ''; row.weaponViewPrewarmSize = 0; row.fireSoundAddress = ''; row.muzzleVfxAddress = ''; row.hitVfxAddress = ''; row.iconAddress = '';
  }
  return row;
});
const oldAmmo = await readRows(`${root}/Assets/GameData/Ammo/ammo.csv`, 'ExistingAmmo');
const ammoIds = { 'Standard Bullet (AR)':2001, 'Rusty Bullet (AR)':2002, 'AP Bullet (AR)':2003,
  'Rusty Bullet (S)':2011, 'Standard Bullet (S)':2012, 'AP Bullet (S)':2013,
  'Rusty Shotgun Shell':2021, 'Standard Shotgun Shell':2022, 'AP Shotgun Shell':2023 };
const ammo = sources.Ammo.map(raw => {
  const id = ammoIds[raw.Name]; assert(id);
  return { ammoId:id, displayName:raw.Name, iconAddress:oldAmmo.find(r => Number(r.ammoId) === id)?.iconAddress ?? '',
    ammoType:raw.Name.includes('Shotgun') ? 'Shotgun' : raw.Name.includes('(S)') ? 'Pistol' : 'Rifle',
    armorPenetrationLevel:raw.Name.startsWith('AP ') ? 2 : raw.Name.startsWith('Standard ') ? 1 : 0,
    damageMultiplier:1, maxStackCount:Number(raw.MaxStack), unitWeight:Number(raw.Weight) };
}).sort((a,b) => a.ammoId-b.ammoId);
const common = (raw,id) => ({itemId:id, displayName:raw.Name, unitWeight:Number(raw.Weight), maxStackCount:Number(raw.MaxStack), iconAddress:''});
const medicine = sources.Medicine.map((raw,i) => ({...common(raw,3001+i), healAmount:Number(raw.HealHP), useDuration:2}));
const armor = sources.Armor.map((raw,i) => ({...common(raw,4001+i), armorLevel:Number(raw.BodyArmorBonus), backpackCapacityBonus:Number(raw.BackpackCapacityBonus || 0)}));
const helmet = sources.Helmet.map((raw,i) => ({...common(raw,5001+i), armorLevel:Number(raw.HeadArmorBonus)}));
const escape = value => {
  const text = String(value ?? '');
  return /[",\r\n]/.test(text) ? '"'+text.replaceAll('"','""')+'"' : text;
};
async function writeCsv(file,name,rows,headers=Object.keys(rows[0])) {
  assert.equal(new Set(rows.map(r => r.weaponId ?? r.ammoId ?? r.itemId)).size, rows.length);
  const wb = Workbook.create(); const sheet = wb.worksheets.add(name);
  sheet.getRangeByIndexes(0,0,rows.length+1,headers.length).values = [headers, ...rows.map(row => headers.map(h => row[h] ?? ''))];
  wb.recalculate();
  const values = sheet.getUsedRange().values;
  assert.equal(values.length, rows.length+1);
  // 문서화된 CSV 내보내기 기능이 없어 검증한 셀 값을 CSV의 인용 규칙에 맞춰 저장한다.
  await fs.mkdir(path.dirname(file), { recursive:true });
  await fs.writeFile(file, values.map(row => row.map(escape).join(',')).join('\r\n')+'\r\n', 'utf8');
  const reread = await readRows(file,name);
  assert.equal(reread.length, rows.length);
  console.log(`${name}: ${rows.length} rows, ${headers.length} columns`);
}
await writeCsv(weaponFile,'Weapons',weapons,[...new Set([...Object.keys(oldWeapons[0]), ...Object.keys(weapons[0])])]);
await writeCsv(`${root}/Assets/GameData/Ammo/ammo.csv`,'Ammo',ammo);
await writeCsv(`${root}/Assets/GameData/Medicine/medicine.csv`,'Medicine',medicine);
await writeCsv(`${root}/Assets/GameData/Armor/armor.csv`,'Armor',armor);
await writeCsv(`${root}/Assets/GameData/Helmet/helmet.csv`,'Helmet',helmet);
