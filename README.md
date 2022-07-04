# AbittiUSB

> Ylioppilastutkintolautakunta julkaisi AbittiUSB: yhdessä Abitin kanssa alkuvuodesta 2015.
> Kun vastaavia ohjelmia alkoi tulla jakeluun eikä YTL itse käyttänyt ohjelmaa ylioppilastutkinnossa
> käytettävien USB-muistien kirjoittamiseen, ohjelman jakelu lopetettiin 20.6.2021.
> AbittiUSB:n käyttö päättyi 20.6.2022, kun poisti palvelimeltaan ohjelman tarvitsemat
> levynkuvat ja niiden versiotiedot.
>
> AbittiUSB on julkaistu kiinnostuneiden tutkittavaksi. YTL ei ylläpidä AbittiUSB:ta.

AbittiUSB on apuväline koe- ja koetilatikkujen polttamiseen.  Se kykenee
lataamaan, verifioimaan ja polttamaan usb tikuille useita rinnakkain, säilyttäen
käyttöliittymän responsiivisuuden.

## Asennus

Asennukseen asiakaskoneille käytetään clickoncea, eri versiot olivat ladattavissa
seuraavista osoitteista (asennuspaketit on poistettu jakelusta 20.6.2021):

* Testi: http://static.abitti.fi/AbittiUSB/test/setup.exe
* QA: http://static.abitti.fi/AbittiUSB/QA/setup.exe
* tuotanto: http://static.abitti.fi/AbittiUSB/prod/setup.exe

## Uuden version julkaisu

Uuden versiot julkaistaan komentoriviltä ajettavilla scripteillä.  Visual
Studion command promptissa (signtoolit, msbuildit yms. ) tai sitten niiden pitää
olla muuten laitettuna polkuun.  Tarvitset Code Signing Certifikaatin, jotta
voit allekirjoittaa julkaisun oikein.

Setup on seuraava:

Verkkolevy (Q:) on mäpätty suoraan Amazoniin oikeaan polkuun (esimerkiksi itse
olen käyttänyt TNTDrivea).  Tarkat osoitteet on määritetty
stick-burner.csproj:ssa.  Polut ovat configuraatioriippuvaisia.  Mäppäykset
configuraatioista asennuslokaatioihin ovat:

* Debug --> TEST
* QA --> QA
* Release --> prod

**Julkaisun parametrina annetaan se releasen numero, joka halutaan julkaista
muodossa `major.minor.build revision`** eli mikäli halutaan viedä version
`1.0.2.4` julkaisu, annetaan skriptille parametrina `1.0.2 4`. **Saman version
pitää olla määritettynä `StickBurner.csproj` tiedoston `ApplicationVersion` ja
`ApplicationRevision` arvoina sekä `App.config` tiedoston `ProductVersion`
arvona.  Lisäksi `StickBurner.csproj` tiedostossa `AssemblyName` propertyn
täytyy vastata configuraatiota.**

* Testijulkaisu: `test_publish.bat major.minor.build revision`
* QA-julkaisu: `qa_publish.bat major.minor.build revision`
* Tuotanto-julkaisu: `production_publish.bat major.minor.build revision`
  * Skripti kysyy vahvistuksen (K/E).

## Kun sertifikaatti vaihtuu

Ennen kuin yrität ajaa `*_publish.bat` -skriptejä varmista, että PATHissa on
`msbuild`, `signtool` ja `mage`.

 1. Exporttaa ja asenna uusi sertifikaatti devauskoneelle.
    https://knowledge.symantec.com/support/ssl-certificates-support/index?page=content&id=SO1532&actp=search&viewlocale=en_US
 2. Katso sertifikaatin SHA1-hash:
    * `certmgr.exe`
    * Certificates - Current User > Personal > Certificates
    * Tuplaklikkaa > Details-välilehti
    * `Thumbprint algorithm` pitää olla "sha1" ja tarvittava hashi on kentässä
      `Thumbprint`
 3. Päivitä uusi hashi julkaisubatteihin `stick-burner/production_publish.bat`,
    `stick-burner/qa_publish.bat` ja `stick-burner/test_publish.bat`.
 4. Päivitä uusi versionumero, jotta kaikkien clientit päivittyvät. Versionumero
    päivitetään tiedostoon `stick-burner/App.config`
 5. Aja `stick-burner/qa_publish.bat` ja varmistu, että päivitysmekanismi toimii
    oikein (vanhat versiot päivittyvät uusiin testikoneilla).

## Lisenssit

AbittiUSB:n lisenssi on tiedostosta `LICENSE`, (C) Ylioppilastutkintolautakunta 2014-2022.

Lisenssi ei kata seuraavia komponentteja:

* `.nuget/`, ks. [NuGet](https://www.nuget.org/)
* `UsbHandlingUtils/tools/`, ks. [USB Image Tool](https://www.alexpage.de/usb-image-tool/)
* `stick-burner/ChildProcessUtil_1_0_7.exe`, ks. [ChildProcessUtil](https://github.com/ramik/ChildProcessUtil)
* `marssiva_kukko.ico`, Ylioppilastutkintolautakunta
