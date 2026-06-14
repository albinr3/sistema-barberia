const supabaseUrl = process.env.NEXT_PUBLIC_SUPABASE_URL!;
const supabaseKey = process.env.NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY!;

async function queryTable(table: string) {
  const res = await fetch(`${supabaseUrl}/rest/v1/${table}?select=*`, {
    headers: {
      apikey: supabaseKey,
      Authorization: `Bearer ${supabaseKey}`,
    },
  });
  return res.json();
}

async function main() {
  const barbers = await queryTable('barbers');
  const services = await queryTable('services');
  const assignments = await queryTable('barber_services');
  const rules = await queryTable('availability_rules');
  
  console.log("--- Barbers ---");
  console.log(barbers);
  console.log("\n--- Services ---");
  console.log(services);
  console.log("\n--- Assignments (Barber Services) ---");
  console.log(assignments);
  console.log("\n--- Rules ---");
  console.log(rules);
}

main().catch(console.error);
