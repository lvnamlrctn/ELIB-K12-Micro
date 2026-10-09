async function run() {
  const tryUrl = async (url) => {
    try {
      let res = await fetch(url);
      console.log("GET", url, res.status);
      if (res.ok) console.log(await res.text().then(t => t.substring(0, 100)));
    } catch(e) {}
  };
  
  await tryUrl("http://103.97.134.58:8090/api/LinkGroup");
  await tryUrl("http://103.97.134.58:8090/api/Class");
}
run();
