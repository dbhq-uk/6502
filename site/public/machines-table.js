// Sorting and filtering for the machines and chips tables.
//
// The tables are complete, ordered HTML without this file. This only adds
// sortable headers and, on the machines table, three filters. It is an external
// module because the site's CSP has no 'unsafe-inline' in script-src.
for (const table of document.querySelectorAll("table[data-sortable]")) {
  const body = table.tBodies[0];
  const rows = () => Array.from(body.rows);
  const headers = Array.from(table.tHead.rows[0].cells);

  headers.forEach((th, column) => {
    const label = th.textContent.trim();
    const button = document.createElement("button");
    button.type = "button";
    button.textContent = label;
    th.textContent = "";
    th.appendChild(button);
    th.setAttribute("aria-sort", "none");
    button.addEventListener("click", () => {
      const ascending = th.getAttribute("aria-sort") !== "ascending";
      headers.forEach((other) => other.setAttribute("aria-sort", "none"));
      th.setAttribute("aria-sort", ascending ? "ascending" : "descending");
      const key = (row) => row.cells[column].dataset.sort ?? row.cells[column].textContent.trim();
      const sorted = rows().sort((a, b) => {
        const x = key(a);
        const y = key(b);
        const numeric = x !== "" && y !== "" && !Number.isNaN(Number(x)) && !Number.isNaN(Number(y));
        const order = numeric ? Number(x) - Number(y) : x.localeCompare(y, "en-GB");
        return ascending ? order : -order;
      });
      body.append(...sorted);
    });
  });
}

const filters = document.querySelectorAll("[data-filter]");
const table = document.querySelector("table[data-filterable]");
const counter = document.querySelector("[data-count]");
if (table && filters.length) {
  const apply = () => {
    let shown = 0;
    for (const row of table.tBodies[0].rows) {
      const visible = Array.from(filters).every((select) => !select.value || row.dataset[select.dataset.filter] === select.value);
      row.hidden = !visible;
      if (visible) shown++;
    }
    if (counter) counter.textContent = `Showing ${shown} of ${table.tBodies[0].rows.length}`;
  };
  filters.forEach((select) => select.addEventListener("change", apply));
  apply();
}
