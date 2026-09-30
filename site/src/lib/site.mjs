export const SITE = {
  url: 'https://6502.dbhq.uk',
  name: '6502',
  repo: 'https://github.com/dbhq-uk/6502',
  privacy: 'https://dbhq.uk/privacy/',
};

export const NAV = [
  { href: '/journal/', label: 'Journal' },
  { href: '/machines/', label: 'Machines' },
  { href: '/chips/', label: 'Chips' },
  { href: '/family/', label: 'Family' },
  { href: '/status/', label: 'Status' },
  { href: '/about/', label: 'About' },
];

/** The non-affiliation line every page's footer carries, word for word. */
export const CAPCOM =
  'The goal is a verified port of Mega Man 2, checked against the original by differential testing. ' +
  'Mega Man is a trademark of Capcom Co., Ltd. This project is not affiliated with, endorsed by or sponsored by Capcom. ' +
  "Nothing of Capcom's is on this site.";

export const longDate = (d) => d.toLocaleDateString('en-GB', { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' });
export const shortDate = (d) => d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' });
