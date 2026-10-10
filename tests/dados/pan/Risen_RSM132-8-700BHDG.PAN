PVObject_=pvModule
  Version=6.88
  Flags=$00D00043

  PVObject_Commercial=pvCommercial
    Comment=www.risenenergy.com
    Flags=$0041
    Manufacturer=Risen Energy Co., Ltd
    Model=RSM132-8-700BHDG
    DataSource=DEKRA Testing and Certification
    YearBeg=2022
    Width=1.303
    Height=2.384
    Depth=0.035
    Weight=40.500
    NPieces=100
    PriceDate=05/03/18 08:38
    Currency=€
  End of PVObject pvCommercial

  Technol=mtHIT
  NCelS=66
  NCelP=2
  NDiode=3
  SubModuleLayout=slTwinHalfCells
  FrontSurface=fsARCoating
  GRef=1000
  TRef=25.0
  PNom=700.0
  PNomTolLow=0.00
  PNomTolUp=3.00
  BifacialityFactor=0.850
  Isc=17.820
  Voc=49.83
  Imp=16.770
  Vmp=41.78
  muISC=8.38
  muVocSpec=-109.6
  muPmpReq=-0.240
  RShunt=1600
  Rp_0=70000
  Rp_Exp=5.50
  RSerie=0.173
  Gamma=1.064
  muGamma=0.0000
  VMaxIEC=1500
  VMaxUL=1500
  Absorb=0.90
  ARev=3.200
  BRev=21.996
  RDiode=0.010
  VRevDiode=-0.70
  AirMassRef=1.500
  CellArea=220.5
  SandiaAMCorr=50.000
  RelEffic800=0.62
  RelEffic600=0.92
  RelEffic400=0.62
  RelEffic200=-0.74

  PVObject_IAM=pvIAM
    Flags=$00
    IAMMode=UserProfile
    IAMProfile=TCubicProfile
      NPtsMax=9
      NPtsEff=9
      LastCompile=$B18D
      Mode=3
      Point_1=0.0,1.00000
      Point_2=20.0,1.00000
      Point_3=40.0,1.00000
      Point_4=60.0,1.00000
      Point_5=70.0,0.99400
      Point_6=75.0,0.97900
      Point_7=80.0,0.94700
      Point_8=85.0,0.85800
      Point_9=90.0,0.00000
    End of TCubicProfile
  End of PVObject pvIAM

  OperPoints, list of=4 tOperPoint
    Point_1=False,800,25.0,0.62,0.00,0.000,0.000,0.00
    Point_2=False,600,25.0,0.92,0.00,0.000,0.000,0.00
    Point_3=False,400,25.0,0.62,0.00,0.000,0.000,0.00
    Point_4=False,200,25.0,-0.74,0.00,0.000,0.000,0.00
  End of List OperPoints
End of PVObject pvModule
