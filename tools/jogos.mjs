// Marks in dex.json which Pokémon can be caught before the Elite Four in each game, from PokéAPI's encounter data.
// Usage: node tools/jogos.mjs <folder with PokéAPI csv files> PaletMon.Core/Assets/dex.json
// The csv files come from https://github.com/PokeAPI/pokeapi/tree/master/data/v2/csv
import fs from 'node:fs';

const [dir, dexPath] = process.argv.slice(2);
const csv = name => {
  const [head, ...rows] = fs.readFileSync(`${dir}/${name}.csv`, 'utf8').trim().split(/\r?\n/);
  const keys = head.split(',');
  return rows.map(r => { const v = r.split(','); return Object.fromEntries(keys.map((k, i) => [k, v[i]])); });
};

// version ids, and the places that only open after the Elite Four (or need the National Dex, Kanto, or the post-game)
const games = {
  pt: {
    versions: ['14'],
    after: ['stark-mountain', 'sinnoh-route-224', 'sinnoh-route-225', 'sinnoh-sea-route-226', 'sinnoh-route-227', 'sinnoh-route-228',
      'sinnoh-route-229', 'sinnoh-sea-route-230', 'resort-area', 'newmoon-island', 'flower-paradise', 'snowpoint-temple',
      'sinnoh-hall-of-origin-1', 'spear-pillar', 'sendoff-spring', 'turnback-cave', 'trophy-garden', 'roaming-sinnoh'],
    fossils: ['skull', 'armor'],
    extra: [],
    maxGen: 4,
    legends: ['giratina', 'uxie', 'azelf'],
  },
  hgss: {
    versions: ['15', '16'],
    // Kanto, Mt. Silver and the post-game places; Route 26, 27, Tohjo Falls and Victory Road lead to the League
    after: ['mt-moon', 'seafoam-islands', 'mt-silver', 'cinnabar-island', 'viridian-city', 'cerulean-city', 'vermilion-city',
      'celadon-city', 'fuchsia-city', 'pallet-town', 'saffron-city', 'pewter-city', 'rock-tunnel', 'digletts-cave', 'viridian-forest',
      'cerulean-cave', 'kanto-power-plant', 'embedded-tower', 'sinjoh-ruins', 'kanto-route-28', 'kanto-sea-route-19',
      'kanto-sea-route-20', 'kanto-sea-route-21', ...[1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 22, 24, 25].map(n => `kanto-route-${n}`)],
    fossils: [],
    extra: [],
    maxGen: 4,
    johtoDex: true, // before the National Dex only lines from the first two generations show up
    legends: ['lugia', 'ho-oh', 'raikou', 'entei'],
    // they evolve only near a Moss Rock, Ice Rock or magnetic field, none of which Johto has
    never: ['leafeon', 'glaceon', 'magnezone', 'probopass'],
  },
  bw: {
    versions: ['17', '18'],
    after: ['giant-chasm', 'p2-laboratory', 'undella-bay', 'undella-town', 'village-bridge', 'marvelous-bridge', 'challengers-cave',
      'abundant-shrine', 'liberty-garden', 'ns-castle', 'trial-chamber', 'guidance-chamber', 'lostlorn-forest', 'team-flare-secret-hq',
      ...[11, 12, 13, 14, 15, 16, 17, 18].map(n => `unova-route-${n}`)],
    fossils: ['cover', 'plume'],
    extra: [],
    maxGen: 5,
    onlyGen: 5, // the Unova Pokédex of Black and White has only fifth generation Pokémon
    legends: [],
  },
  b2w2: {
    versions: ['21', '22'],
    after: ['p2-laboratory', 'clay-tunnel', 'underground-ruins', 'rocky-mountain-room', 'glacier-room', 'iron-room', 'nature-sanctuary',
      'marvelous-bridge', 'abundant-shrine', 'guidance-chamber', 'unova-route-14', 'unova-route-15', 'unova-route-17', 'unova-route-18'],
    fossils: ['cover', 'plume'],
    extra: [],
    maxGen: 5,
    legends: [],
  },
  xy: {
    versions: ['23', '24'],
    after: ['sea-spirits-den', 'unknown-dungeon', 'friend-safari', 'roaming-kalos'],
    afterStatic: ['terminus-cave'], // the cave is open before the League, Zygarde only after
    fossils: [],
    extra: ['tyrunt', 'amaura'], // Glittering Cave fossils
    maxGen: 6,
    legends: ['xerneas', 'yveltal'],
  },
  usum: {
    versions: ['29', '30'],
    after: ['resolution-cave', 'ultra-space', 'ultra-space-wilds', 'team-rockets-castle', 'poke-pelago', 'lake-of-the-sunne',
      'lake-of-the-moone', 'ruins-of-abundance', 'ruins-of-conflict', 'ruins-of-hope', 'ruins-of-life'],
    fossils: [],
    extra: ['cranidos', 'shieldon', 'tirtouga', 'archen', 'tyrunt', 'amaura'], // sold on Route 8, one pair per version
    maxGen: 7,
    legends: ['necrozma'],
    region: 'Alola', // where a Pokémon has an Alolan form, that is the one found and evolved in Alola
    // Ultra Beasts and Type: Null only come after the League
    never: ['type-null', 'silvally', 'poipole', 'naganadel', 'stakataka', 'blacephalon', 'nihilego', 'buzzwole', 'pheromosa',
      'xurkitree', 'celesteela', 'kartana', 'guzzlord'],
  },
};

// conditions that need the post-game, the National Dex, a GBA cartridge, an event or a Pokémon you could not have yet
const blocked = new Set([
  'slot2-ruby', 'slot2-sapphire', 'slot2-emerald', 'slot2-firered', 'slot2-leafgreen', 'radar-on', 'swarm-yes', 'radio-hoenn', 'radio-sinnoh',
  'story-progress-hall-of-fame', 'story-progress-national-dex', 'other-snorlax-11-beat-league', 'story-progress-beat-red',
  'other-received-kanto-starter', 'other-regirock-regice-registeel-in-party', 'other-event-arceus-in-party', 'item-adamant-orb',
  'item-lustrous-orb', 'other-giratina-not-caught-in-distortion-world', 'item-ice-key', 'item-iron-key', 'item-lunar-wing',
  'item-light-stone', 'item-dark-stone', 'other-captured-reshiram-or-zekrom', 'defeated-ghetsis', 'special-encounter-couldnt-capture-before',
  'story-progress-juniper-cave-of-being', 'other-found-11-times-roaming', 'other-raikou-entei-in-party', 'other-groudon-kyogre-in-party',
  'other-dialga-palkia-in-party', 'other-tornadus-thundurus-in-party', 'other-reshiram-zekrom-in-party', 'other-scan-qr-code',
  'friend-safari-slot-1', 'friend-safari-slot-2', 'friend-safari-slot-3', 'story-progress-vermilion-copycat',
  'story-progress-returned-machine-part', 'story-progress-oak-eterna-city', 'story-progress-cure-eldritch-nightmares',
  'backlot-mentioned', // Trophy Garden's daily Pokémon need the National Dex
]);
const fossilItems = ['helix', 'dome', 'old-amber', 'root', 'claw', 'skull', 'armor', 'cover', 'plume'];
const blockedMethods = new Set(['island-scan']);

const areas = Object.fromEntries(csv('location_areas').map(a => [a.id, a.location_id]));
const locations = Object.fromEntries(csv('locations').map(l => [l.id, l.identifier]));
const slots = Object.fromEntries(csv('encounter_slots').map(s => [s.id, s.encounter_method_id]));
const methods = Object.fromEntries(csv('encounter_methods').map(m => [m.id, m.identifier]));
const conditionNames = Object.fromEntries(csv('encounter_condition_values').map(v => [v.id, v.identifier]));
const conditions = {};
for (const m of csv('encounter_condition_value_map')) (conditions[m.encounter_id] ??= []).push(conditionNames[m.encounter_condition_value_id]);
const speciesOf = Object.fromEntries(csv('pokemon').map(p => [p.id, p.species_id]));
const speciesId = Object.fromEntries(csv('pokemon_species').map(s => [s.identifier, s.id]));
// a species counts as reachable by evolving unless every way to get it is a trade
const evolveWays = {};
for (const e of csv('pokemon_evolution')) (evolveWays[e.evolved_species_id] ??= new Set()).add(e.evolution_trigger_id);
const tradeOnly = id => evolveWays[id] && [...evolveWays[id]].every(t => t === '2');

const dex = JSON.parse(fs.readFileSync(dexPath, 'utf8'));
const byId = new Map(dex.pokemon.map(p => [p.i, p]));
const region = p => p.r ? ['Alola', 'Galar', 'Hisui', 'Paldea'].find(r => p.n.includes(r)) ?? null : null;
const kidsOf = p => {
  const kids = dex.pokemon.filter(k => k.f === p.s && k.s !== p.s);
  const reg = region(p);
  if (reg) { const same = kids.filter(k => region(k) === reg); return same.length ? same : kids.filter(k => !k.r); }
  return kids.filter(k => !k.r);
};

const encounters = csv('encounters');
for (const [code, g] of Object.entries(games)) {
  const caught = new Set();
  const add = id => {
    if (byId.has(id)) { caught.add(id); return; }
    const base = byId.get(Number(speciesOf[id])); // a form the app does not list: count the regular one
    if (base) caught.add(base.i);
  };
  for (const e of encounters) {
    if (!g.versions.includes(e.version_id)) continue;
    const place = locations[areas[e.location_area_id]];
    const method = methods[slots[e.encounter_slot_id]];
    if (g.after.includes(place) || blockedMethods.has(method)) continue;
    if (g.afterStatic?.includes(place) && method === 'only-one' || g.afterStatic?.includes(place) && method === 'static') continue;
    const conds = conditions[e.id] ?? [];
    if (conds.some(c => blocked.has(c))) continue;
    const fossil = conds.find(c => fossilItems.some(f => c === `item-${f}-fossil` || c === `item-${f}`));
    if (fossil && !g.fossils.some(f => fossil === `item-${f}-fossil`)) continue;
    add(Number(e.pokemon_id));
  }
  for (const name of g.extra) add(Number(speciesId[name]));
  // legendaries only where the story hands them over before the League
  const allowed = new Set(g.legends.map(n => Number(speciesId[n])));
  const never = new Set((g.never ?? []).map(n => Number(speciesId[n])));
  for (const id of [...caught]) {
    const p = byId.get(id);
    if ((p.L || p.M) && !allowed.has(p.s) || never.has(p.s)) caught.delete(id);
  }
  // evolutions of what you caught, except the ones that need a trade
  const queue = [...caught];
  while (queue.length) {
    for (const k of kidsOf(byId.get(queue.shift()))) {
      if (caught.has(k.i) || tradeOnly(String(k.s)) || k.g > g.maxGen || never.has(k.s)) continue;
      caught.add(k.i);
      queue.push(k.i);
    }
  }
  if (g.region) for (const id of [...caught]) {
    const local = dex.pokemon.find(p => p.n === `${byId.get(id).n} ${g.region}`);
    if (local) { caught.delete(id); caught.add(local.i); }
  }
  if (g.onlyGen) for (const id of [...caught]) if (byId.get(id).g !== g.onlyGen) caught.delete(id);
  if (g.johtoDex) {
    const oldLines = new Set(dex.pokemon.filter(p => p.g <= 2).map(p => p.c));
    for (const id of [...caught]) if (!oldLines.has(byId.get(id).c)) caught.delete(id);
  }
  for (const p of dex.pokemon) {
    p.v = (p.v ?? []).filter(x => x !== code);
    if (caught.has(p.i)) p.v.push(code);
  }
  console.log(code, caught.size);
}
const order = Object.keys(games);
for (const p of dex.pokemon) {
  p.v.sort((a, b) => order.indexOf(a) - order.indexOf(b));
  if (!p.v.length) delete p.v;
}
fs.writeFileSync(dexPath, JSON.stringify(dex));
