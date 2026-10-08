import fs from 'node:fs/promises';
import { Workbook } from '@oai/artifact-tool';

// 원본 CSV를 메모리에만 읽어 열과 값의 형태를 확인한다. 원본을 저장하거나 변환하지 않는다.
for (const name of ['Weapons', 'Ammo', 'Medicine', 'Armor', 'Helmet']) {
  const path = `C:/Users/admin/Desktop/에셋/${name}/${name}.csv`;
  const csv = await fs.readFile(path, 'utf8');
  const workbook = await Workbook.fromCSV(csv, { sheetName: name });
  const values = workbook.worksheets.getItem(name).getUsedRange().values;
  const headers = values[0];
  const rows = values.slice(1).filter(row => row.some(value => value !== null && value !== ''));
  const columns = headers.map((header, index) => ({
    header,
    empty: rows.filter(row => row[index] === null || row[index] === '').length,
    values: [...new Set(rows.map(row => row[index]))],
  }));
  console.log(JSON.stringify({ path, rowCount: rows.length, columnCount: headers.length, columns }));
}
