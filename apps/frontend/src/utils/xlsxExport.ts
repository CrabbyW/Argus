/**
 * Skutečný sešit .xlsx, ne CSV přejmenované na Excel.
 *
 * CSV se v Excelu láme na národním nastavení — oddělovač, kódování, datum jako text — a sešit
 * navíc unese to, co se od exportu inventáře čeká: zmrazený a tučný záhlaví, autofiltr a šířky
 * sloupců podle obsahu. Píše se přes ExcelJS, který se načítá až při prvním exportu
 * (`await import`), aby knihovna nebyla v úvodním bundlu kvůli tlačítku, na které se většinou
 * neklikne.
 */
export type SheetColumn = {
  header: string;
  /** Šířka ve znacích. Excel nemá "auto" v souboru — šířku musí zapsat ten, kdo sešit tvoří. */
  width: number;
};

export async function downloadXlsx(
  fileName: string,
  sheetName: string,
  columns: SheetColumn[],
  rows: unknown[][],
) {
  const { default: ExcelJS } = await import('exceljs');

  const workbook = new ExcelJS.Workbook();

  workbook.creator = 'Argus';
  workbook.created = new Date();

  // Název listu Excel omezuje na 31 znaků a zakazuje v něm : \ / ? * [ ].
  const sheet = workbook.addWorksheet(sheetName.replace(/[:\\/?*[\]]/g, ' ').slice(0, 31));

  sheet.columns = columns.map((column) => ({ header: column.header, width: column.width }));
  sheet.addRows(rows);

  sheet.getRow(1).font = { bold: true };
  // Záhlaví zůstává vidět při rolování a nese filtry — sešit se otevře rovnou připravený
  // k tomu, co s ním člověk stejně udělá: seřadit a profiltrovat.
  sheet.views = [{ state: 'frozen', ySplit: 1 }];
  sheet.autoFilter = {
    from: { row: 1, column: 1 },
    to: { row: rows.length + 1, column: columns.length },
  };

  const buffer = await workbook.xlsx.writeBuffer();
  const blob = new Blob([buffer], {
    type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');

  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

/** `argus-installations-2026-08-15.xlsx` — datum v názvu, ať se stažené exporty nepřepisují. */
export function timestampedFileName(prefix: string, extension = 'xlsx') {
  const now = new Date();
  const pad = (value: number) => String(value).padStart(2, '0');

  return `${prefix}-${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}.${extension}`;
}
