const months=['jan','feb','mar','apr','may','jun','jul','aug','sep','oct','nov','dec'];
export function readGameCalendar(frame) {
  const read=frame.detail?.calendarRead;
  if(read&&Number.isInteger(read.year)&&read.year>=1100&&read.year<=1299&&Number.isInteger(read.month)&&read.month>=1&&read.month<=12)return {year:read.year,month:read.month};
  const years=new Set(),seenMonths=new Set();
  for(const l of frame.detail?.ocr?.available?frame.detail.ocr.lines||[]:[]) {
    if(!l.words?.length||!l.words.every(w=>w.x>=0&&w.x<250&&w.y>=0&&w.y<200))continue;
    for(const y of String(l.text).match(/\b1[12]\d{2}\b/g)||[])years.add(Number(y));
    const m=String(l.text).match(/\b(Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:tember)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\b/i);
    if(m)seenMonths.add(months.indexOf(m[1].slice(0,3).toLowerCase())+1);
  }
  return {year:years.size===1?[...years][0]:null,month:seenMonths.size===1?[...seenMonths][0]:null};
}
