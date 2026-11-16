# MoonDrop Meadows: Design Bible

## The promise

The player does not arrive in town to become a farmer. They arrive to become somebody.

MoonDrop Meadows is a long-running 2D pixel-art life sandbox. A save can begin with a child walking to school, a teenager trying a first job, or an adult starting over. The town keeps its own routines. People remember choices. Relationships deepen over years. A small decision can become a family tradition.

## Character and lifetime

- Choose a name and starting age from 8 to 80.
- Choose a family setup: two parents with an optional sibling in the initial household.
- Future creator options: appearance, pronouns, personality strengths, voice, hometown history, and difficulty.
- Starting age changes the opening chapter, available activities, school access, work options, and social circles.
- Players under 18 have school, family, friends, clubs, and age-appropriate part-time activities. Romance is locked until adulthood.
- Birthdays advance with in-game years. Children grow through baby, toddler, child, teen, and adult stages. Their dialogue, appearance, interests, and independence change.
- Adult children can leave home, visit, take jobs, start families, or move back. The family tree and scrapbook preserve generations.

## Relationships

Every resident has a memory and relationship profile.

- Friendship grows through conversation, thoughtful gifts, shared activities, requests, and remembering important dates.
- Dating is an activity, not a button that instantly grants a relationship.
- Dating can become committed partnership and marriage when both characters are ready.
- Marriage is optional. Living together, co-parenting, and remaining close friends are valid outcomes.
- Children inherit personality and interest traits from their family.
- Breakups, reconciliation, anniversaries, birthdays, family arguments, and visits are planned systems.
- Romance candidates are adults. Family members and minors are never romance candidates.

## Town districts

1. Meadow Lane: starter home, gardens, quiet neighbors, and old footbridge.
2. Market Row: general store, clothing shop, bakery, cafe, repair counter, and weekend market.
3. Civic Green: town square, mayor's office, library, community hall, and newspaper kiosk.
4. School Hill: elementary school, high school, playground, sports field, and adult learning center.
5. Moonwood: Bob's tower, strange plants, caves, forest trails, and the Moonwell.
6. Riverside: fishing spots, mill, parks, and land for development.
7. Workshop Quarter: rentable storefronts, warehouses, maker spaces, and business lots.

Residents follow daily schedules. Shops open and close. Students attend school. Workers commute. The square hosts dances, markets, concerts, sports days, and seasonal festivals.

## Careers and businesses

Farming is a complete career option, not a requirement.

Careers include farmer, rancher, beekeeper, programmer, electronics repairer, chef, baker, builder, carpenter, architect, artist, photographer, musician, teacher, librarian, veterinarian, shopkeeper, courier, journalist, and event planner.

Player-owned companies:
- Buy land or lease a shop, workshop, farm, or office.
- Choose company type, name, products, prices, hours, and visual identity.
- Hire residents already living in town.
- Post jobs online and recruit people who may relocate to MoonDrop Meadows.
- Employees have skills, preferences, schedules, wages, morale, and relationships.
- Train staff, promote managers, expand premises, and open additional locations.
- Build supply chains between farms, manufacturers, shops, and cafes.
- Manage closely or delegate daily operations.

## The home computer

The computer is a useful town hub, not a menu disguised as a computer.

- Inbox: family messages, letters, work mail, invitations, and story clues.
- MeadowNet: local social feed, posts, comments, rumors, and event photos.
- Town Ledger: local news, schedules, public notices, and newspaper archive.
- Job Board: applications, shifts, careers, and training.
- MarketSquare: supplies, clothing, furniture, seeds, and business equipment.
- Business Desk: company dashboard, payroll, staff roster, inventory, and sales.
- LandLink: buy or lease property, browse lots, and list homes.
- Bank: savings, household budget, loans, and business finance.
- Design Studio: signs, room layouts, clothing palettes, and business branding.
- Town Map: public locations, shops, transport, and discovered places.

Online-style features are simulated locally in single-player mode. No real internet is required.

## Newspaper and town memory

The MoonDrop Gazette publishes a new issue each morning. It can report on town council decisions, MAYORLARRY's announcements, school plays, concerts, community dances, new shops, business openings, jobs, residents moving in, player milestones, festival prizes, public projects, and rumors about Bob and the Moonwell. The archive becomes a timeline of the player's town.

## Farming and land

Crop seasons, soil quality, watering, fertilizer, pests, harvest quality, orchards, greenhouses, barns, coops, livestock, adjacent land purchases, farmhands, irrigation, storage, packing sheds, delivery routes, farm shops, and wholesale supply contracts.

## Clothing and events

Clothing shops carry seasonal collections, formalwear, uniforms, workwear, costumes, and festival outfits. Players customize colors and save outfits. Town dances include music, simple rhythm inputs, partner choices, conversations, photographs, and memories.

## Accessibility and input

Keyboard and mouse on PC, controller navigation and remappable controls, touch controls with a virtual stick and large interaction buttons, scalable UI, readable text, reduced-flash option, and independent music/voice sliders. Menus must work on small screens.

## Technical direction

The simulation and save models stay separate from rendering and input. Platform projects can share world rules while changing windowing, input, audio, and packaging. The current prototype uses MonoGame DesktopGL. Future mobile projects and an authorized Xbox target require separate platform engineering. UWP/Windows 10 Mobile is treated as a legacy compatibility experiment, not the default architecture.
